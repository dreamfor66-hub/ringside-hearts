"""NovelAI image generation for Ringside Hearts.

Cost policy: normal size (<= 1024*1024 px) and steps <= 28. Images per request
default to 1 (--samples 2..4 is available for comparing cost). The script checks
the Anlas balance before and after the first request of a run and stops if
anything was spent, unless --allow-cost is given.

The token is read from the NOVELAI_TOKEN environment variable (process first,
then the Windows user environment in the registry). It is never printed.

Usage (run from the repository root):
  python Tools/NovelAI/nai_gen.py status
  python Tools/NovelAI/nai_gen.py gen longwei standing --count 10
  python Tools/NovelAI/nai_gen.py sheet ArtRaw/longwei/standing
  python Tools/NovelAI/nai_gen.py readmeta path/to/original.png
"""

import argparse
import io
import json
import os
import random
import sys
import time
import urllib.error
import urllib.request
import zipfile
from datetime import datetime
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
CONFIG = HERE / "prompts.json"
RAW_ROOT = REPO / "ArtRaw"

# NovelAI moved account endpoints to the image host; api.novelai.net answers 400 "update to the image URL".
SUB_URL = "https://image.novelai.net/user/subscription"
GEN_URL = "https://image.novelai.net/ai/generate-image"
# Cloudflare rejects Python's default User-Agent (error 1010), so send a plain tool name.
USER_AGENT = "RingsideHeartsTool/0.1"

FREE_MAX_PIXELS = 1024 * 1024
FREE_MAX_STEPS = 28


def get_token():
    token = os.environ.get("NOVELAI_TOKEN")
    if not token and sys.platform == "win32":
        try:
            import winreg
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as key:
                token, _ = winreg.QueryValueEx(key, "NOVELAI_TOKEN")
        except OSError:
            token = None
    if not token:
        sys.exit("NOVELAI_TOKEN is not set (checked process env and Windows user env).")
    return token.strip()


def request(url, token, body=None, timeout=180):
    headers = {"Authorization": f"Bearer {token}", "Accept": "*/*", "User-Agent": USER_AGENT}
    data = None
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, headers=headers, method="POST" if data else "GET")
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return resp.read()


def subscription(token):
    return json.loads(request(SUB_URL, token))


def anlas(sub):
    steps = sub.get("trainingStepsLeft", {}) or {}
    return int(steps.get("fixedTrainingStepsLeft", 0)) + int(steps.get("purchasedTrainingSteps", 0))


def cmd_status(_args):
    token = get_token()
    sub = subscription(token)
    tiers = {0: "Paper", 1: "Tablet", 2: "Scroll", 3: "Opus"}
    perks = sub.get("perks", {}) or {}
    print(f"tier: {tiers.get(sub.get('tier'), sub.get('tier'))}  active: {sub.get('active')}")
    print(f"anlas: {anlas(sub)}")
    print(f"unlimitedImageGeneration: {perks.get('unlimitedImageGeneration')}")
    print(f"unlimitedImageGenerationLimits: {perks.get('unlimitedImageGenerationLimits')}")


def load_config():
    with open(CONFIG, encoding="utf-8") as f:
        return json.load(f)


def build_prompt(cfg, char_id, variant, with_reference=False):
    chars = cfg["characters"]
    if char_id not in chars:
        sys.exit(f"unknown character '{char_id}'. known: {', '.join(chars)}")
    ch = chars[char_id]
    if variant not in ch or variant in ("core", "details"):
        sys.exit(f"'{char_id}' has no '{variant}' tags. known: {', '.join(k for k in ch if k not in ('core', 'details'))}")
    body = [ch["core"]]
    if not with_reference:
        # the user drops body/costume tags once a character reference carries them
        body.append(ch.get("details", ""))
    body.append(ch[variant])
    parts = []
    if cfg.get("furry_mode", False):
        parts.append("fur dataset")
    parts += [cfg.get("common_prefix", "")] + body + [cfg.get("quality_tags", "")]
    prompt = ", ".join(p.strip().strip(",") for p in parts if p and p.strip())
    # the suffix starts with a blank line and a comma, exactly like the user's prompts
    return prompt + cfg.get("common_suffix", "")


def build_negative(cfg, variant):
    extra = (cfg.get("variant_negative", {}) or {}).get(variant, "")
    return ", ".join(x for x in [cfg.get("negative", ""), extra] if x)


REF_SIZES = [(1024, 1536), (1536, 1024), (1472, 1472)]


def encode_reference(path):
    """Fit the reference into the closest allowed canvas, pad with black, return base64 PNG."""
    import base64
    from PIL import Image

    im = Image.open(path).convert("RGB")
    ratio = im.width / im.height
    tw, th = min(REF_SIZES, key=lambda s: abs(s[0] / s[1] - ratio))
    scale = min(tw / im.width, th / im.height)
    resized = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
    canvas = Image.new("RGB", (tw, th), "black")
    canvas.paste(resized, ((tw - resized.width) // 2, (th - resized.height) // 2))
    buf = io.BytesIO()
    canvas.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode("ascii")


def add_reference(body, ref_b64, ref_type, strength, secondary):
    params = body["parameters"]
    params["director_reference_images"] = [ref_b64]
    params["director_reference_descriptions"] = [
        {"caption": {"base_caption": ref_type, "char_captions": []}, "legacy_uc": False}
    ]
    params["director_reference_information_extracted"] = [1]
    params["director_reference_strength_values"] = [strength]
    params["director_reference_secondary_strength_values"] = [secondary]


def build_body(cfg, prompt, seed, samples=1, negative=None):
    p = cfg["params"]
    if negative is None:
        negative = cfg.get("negative", "")
    return {
        "input": prompt,
        "model": cfg["model"],
        "action": "generate",
        "parameters": {
            "params_version": 3,
            "width": p["width"],
            "height": p["height"],
            "scale": p["scale"],
            "sampler": p["sampler"],
            "steps": p["steps"],
            "n_samples": samples,
            "ucPreset": p.get("ucPreset", 0),
            "qualityToggle": p.get("qualityToggle", False),
            "autoSmea": False,
            "dynamic_thresholding": False,
            "controlnet_strength": 1,
            "legacy": False,
            "add_original_image": True,
            "cfg_rescale": p.get("cfg_rescale", 0),
            "noise_schedule": p.get("noise_schedule", "karras"),
            "legacy_v3_extend": False,
            "skip_cfg_above_sigma": None,
            "use_coords": False,
            "legacy_uc": False,
            "normalize_reference_strength_multiple": True,
            "seed": seed,
            "characterPrompts": [],
            "v4_prompt": {
                "caption": {"base_caption": prompt, "char_captions": []},
                "use_coords": False,
                "use_order": True,
            },
            "v4_negative_prompt": {
                "caption": {"base_caption": negative, "char_captions": []},
                "legacy_uc": False,
            },
            "negative_prompt": negative,
            "deliberate_euler_ancestral_bug": False,
            "prefer_brownian": True,
        },
    }


def check_free(cfg):
    p = cfg["params"]
    problems = []
    if p["width"] * p["height"] > FREE_MAX_PIXELS:
        problems.append(f"size {p['width']}x{p['height']} > {FREE_MAX_PIXELS} px")
    if p["steps"] > FREE_MAX_STEPS:
        problems.append(f"steps {p['steps']} > {FREE_MAX_STEPS}")
    return problems


def generate_one(token, body):
    delay = 5
    for attempt in range(6):
        try:
            raw = request(GEN_URL, token, body)
            with zipfile.ZipFile(io.BytesIO(raw)) as z:
                return [z.read(n) for n in sorted(z.namelist()) if n.lower().endswith(".png")]
        except urllib.error.HTTPError as e:
            msg = e.read().decode("utf-8", "replace")[:300]
            if e.code == 429 and attempt < 5:
                print(f"  busy (429), waiting {delay}s")
                time.sleep(delay)
                delay = min(delay * 2, 60)
                continue
            sys.exit(f"HTTP {e.code}: {msg}")
    sys.exit("gave up after repeated 429 responses")


def cmd_gen(args):
    cfg = load_config()
    problems = check_free(cfg)
    if problems and not args.allow_cost:
        sys.exit("settings are outside the free conditions: " + "; ".join(problems) + " (use --allow-cost to override)")
    if args.pose:
        # one-off exploration: use these tags as the variant without saving them to prompts.json
        cfg["characters"].setdefault(args.char, {})[args.variant] = args.pose
    if args.neg:
        cfg.setdefault("variant_negative", {})[args.variant] = args.neg
    prompt = build_prompt(cfg, args.char, args.variant, with_reference=bool(args.ref))
    negative = build_negative(cfg, args.variant)
    if args.dry_run:
        print(json.dumps(build_body(cfg, prompt, 12345, args.samples, negative), ensure_ascii=False, indent=2))
        return
    ref_b64 = encode_reference(args.ref) if args.ref else None
    init_b64 = None
    if args.init:
        import base64
        from PIL import Image
        init_img = Image.open(args.init).convert("RGB").resize((cfg["params"]["width"], cfg["params"]["height"]))
        buf = io.BytesIO()
        init_img.save(buf, format="PNG")
        init_b64 = base64.b64encode(buf.getvalue()).decode("ascii")
        print(f"img2img from: {args.init} (strength {args.init_strength})")
    token = get_token()
    out_dir = RAW_ROOT / args.char / (args.variant + (f"_{args.tag}" if args.tag else ""))
    out_dir.mkdir(parents=True, exist_ok=True)
    if args.ref:
        print(f"reference: {args.ref} ({args.ref_type}, strength {args.ref_strength}, secondary {args.ref_secondary})")

    start = anlas(subscription(token))
    print(f"anlas before: {start}")
    print(f"prompt: {prompt}")
    print(f"requests: {args.count}, images per request: {args.samples}")

    saved = 0
    for i in range(args.count):
        seed = random.randint(1, 2**32 - 1)
        body = build_body(cfg, prompt, seed, args.samples, negative)
        if ref_b64:
            add_reference(body, ref_b64, args.ref_type, args.ref_strength, args.ref_secondary)
        if init_b64:
            body["action"] = "img2img"
            body["parameters"].update({"image": init_b64, "strength": args.init_strength, "noise": 0,
                                       "extra_noise_seed": seed})
        pngs = generate_one(token, body)
        stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        for j, png in enumerate(pngs):
            # per-image seeds of a multi-image request are not known here; keep the request seed and index
            suffix = f"{seed}" if len(pngs) == 1 else f"{seed}-{j}"
            path = out_dir / f"{stamp}_{suffix}.png"
            path.write_bytes(png)
            meta = {"char": args.char, "variant": args.variant, "request_seed": seed, "index": j,
                    "samples": args.samples, "model": cfg["model"], "prompt": prompt,
                    "negative": negative, "params": cfg["params"],
                    "reference": ({"file": str(args.ref), "type": args.ref_type, "strength": args.ref_strength,
                                   "secondary": args.ref_secondary} if args.ref else None)}
            path.with_suffix(".json").write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
            saved += 1
        print(f"[{i + 1}/{args.count}] saved {len(pngs)} image(s), total {saved}")

        if i == 0:
            after = anlas(subscription(token))
            spent = start - after
            print(f"anlas after first request: {after} (spent {spent})")
            if spent > 0 and not args.allow_cost:
                sys.exit("the first request cost Anlas, stopping. Re-run with --allow-cost to continue anyway.")
        time.sleep(args.delay)

    end = anlas(subscription(token))
    print(f"done. {saved} images. anlas after: {end} (spent {start - end} in total)")


def make_mask(size, box, pad=0):
    """White rectangle (repaint area) on black, snapped to the 8px latent grid."""
    import base64
    from PIL import Image, ImageDraw

    w, h = size
    x0, y0, x1, y1 = box
    x0, y0 = max(0, (x0 - pad) // 8 * 8), max(0, (y0 - pad) // 8 * 8)
    x1, y1 = min(w, -(-(x1 + pad) // 8) * 8), min(h, -(-(y1 + pad) // 8) * 8)
    mask = Image.new("RGB", (w, h), "black")
    ImageDraw.Draw(mask).rectangle([x0, y0, x1 - 1, y1 - 1], fill="white")
    buf = io.BytesIO()
    mask.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode("ascii"), (x0, y0, x1, y1)


def cmd_inpaint(args):
    import base64
    from PIL import Image

    cfg = load_config()
    prompt = build_prompt(cfg, args.char, args.variant, with_reference=True)
    if args.extra:
        prompt = prompt.replace(cfg.get("common_suffix", ""), "") + ", " + args.extra + cfg.get("common_suffix", "")
    negative = ", ".join(x for x in [build_negative(cfg, args.variant), args.neg] if x)
    src = Image.open(args.image).convert("RGB")
    buf = io.BytesIO()
    src.save(buf, format="PNG")
    img_b64 = base64.b64encode(buf.getvalue()).decode("ascii")
    mask_b64, box = make_mask(src.size, args.box, args.pad)
    token = get_token()
    out_dir = RAW_ROOT / args.char / f"{args.variant}_fix"
    out_dir.mkdir(parents=True, exist_ok=True)
    start = anlas(subscription(token))
    print(f"anlas before: {start}  mask box: {box}")
    print(f"prompt: {prompt}")
    for i in range(args.count):
        seed = random.randint(1, 2**32 - 1)
        body = build_body(cfg, prompt, seed, 1, negative)
        body["model"] = cfg.get("inpaint_model", cfg["model"] + "-inpainting")
        body["action"] = "infill"
        params = body["parameters"]
        params.update({"image": img_b64, "mask": mask_b64, "strength": args.strength, "noise": 0,
                       "extra_noise_seed": seed, "add_original_image": True,
                       "inpaintImg2ImgStrength": args.strength})
        pngs = generate_one(token, body)
        stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        path = out_dir / f"{stamp}_{seed}.png"
        path.write_bytes(pngs[0])
        meta = {"char": args.char, "variant": args.variant, "inpaint_of": str(args.image), "box": box,
                "seed": seed, "strength": args.strength, "prompt": prompt, "negative": negative}
        path.with_suffix(".json").write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"[{i + 1}/{args.count}] {path.name}")
        if i == 0:
            after = anlas(subscription(token))
            print(f"anlas after first request: {after} (spent {start - after})")
            if start - after > 0 and not args.allow_cost:
                sys.exit("inpainting cost Anlas, stopping. Re-run with --allow-cost to continue.")
        time.sleep(args.delay)
    print(f"done. anlas after: {anlas(subscription(token))}")


def edges_touched(path, band=3, dark=90, min_pixels=15):
    """Edges where dark outline pixels (boots, line art) reach the frame, i.e. the figure is cut off.

    Light effects (wind, fire) may touch the frame freely; only dark line art counts.
    Checked against Longwei b2/b4, which the user flagged as cropped.
    """
    from PIL import Image
    import numpy as np

    a = np.asarray(Image.open(path).convert("L"))
    sides = {"top": a[:band], "bottom": a[-band:], "left": a[:, :band], "right": a[:, -band:]}
    return [k for k, v in sides.items() if int((v < dark).sum()) > min_pixels]


def cmd_sheet(args):
    from PIL import Image, ImageDraw

    folder = Path(args.folder)
    files = sorted(folder.glob("*.png"))
    if args.last:
        files = files[-args.last:]
    if not files:
        sys.exit("no png files")
    cols = args.cols
    thumb_w = args.width
    thumbs = []
    for f in files:
        im = Image.open(f).convert("RGB")
        h = int(im.height * thumb_w / im.width)
        thumbs.append(im.resize((thumb_w, h)))
    th = max(t.height for t in thumbs)
    rows = (len(thumbs) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * thumb_w, rows * (th + 28)), "white")
    draw = ImageDraw.Draw(sheet)
    for idx, t in enumerate(thumbs):
        x, y = (idx % cols) * thumb_w, (idx // cols) * (th + 28)
        sheet.paste(t, (x, y + 28))
        cut = edges_touched(files[idx])
        label = f"{idx + 1}: {files[idx].stem.split('_')[-1]}" + (f"  CUT {'/'.join(cut)}" if cut else "")
        draw.text((x + 6, y + 6), label, fill="red" if cut else "black")
    out = folder / "_sheet.jpg"
    sheet.save(out, quality=85)
    print(out)


def read_stealth(path):
    """NovelAI hides generation info in the alpha channel LSBs (column-major). Survives lossless webp."""
    import gzip
    from PIL import Image

    im = Image.open(path)
    if "A" not in im.getbands():
        return None
    w, h = im.size
    alpha = im.getchannel("A").tobytes()

    def bits():
        for x in range(w):
            for y in range(h):
                yield alpha[y * w + x] & 1

    it = bits()

    def take(nbytes):
        out = bytearray()
        for _ in range(nbytes):
            v = 0
            for _ in range(8):
                v = (v << 1) | next(it)
            out.append(v)
        return bytes(out)

    sig = take(15)
    if sig not in (b"stealth_pngcomp", b"stealth_pnginfo"):
        return None
    length = int.from_bytes(take(4), "big")
    data = take(length // 8)
    if sig == b"stealth_pngcomp":
        data = gzip.decompress(data)
    return json.loads(data.decode("utf-8"))


def cmd_readmeta(args):
    from PIL import Image

    stealth = read_stealth(args.png)
    if stealth:
        comment = stealth.get("Comment")
        print(json.dumps(json.loads(comment) if comment else stealth, ensure_ascii=False, indent=2))
        return
    im = Image.open(args.png)
    info = dict(im.info)
    comment = info.get("Comment")
    if comment:
        try:
            data = json.loads(comment)
            print(json.dumps(data, ensure_ascii=False, indent=2))
            return
        except json.JSONDecodeError:
            pass
    for k, v in info.items():
        if isinstance(v, str):
            print(f"{k}: {v[:2000]}")


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("status")
    g = sub.add_parser("gen")
    g.add_argument("char")
    g.add_argument("variant")
    g.add_argument("--count", type=int, default=1, help="number of requests")
    g.add_argument("--samples", type=int, default=1, choices=[1, 2, 3, 4], help="images per request")
    g.add_argument("--delay", type=float, default=1.0)
    g.add_argument("--allow-cost", action="store_true")
    g.add_argument("--dry-run", action="store_true", help="print the request body without calling the API")
    g.add_argument("--ref", help="character reference image (V4.5 only)")
    g.add_argument("--ref-type", default="character&style", choices=["character", "style", "character&style"])
    # defaults copied from the user's own awakened generations (strength 0.8-0.9, secondary 0.1-0.25)
    g.add_argument("--ref-strength", type=float, default=0.85)
    g.add_argument("--ref-secondary", type=float, default=0.15, help="director_reference_secondary_strengths")
    g.add_argument("--tag", default="", help="suffix for the output folder, e.g. 'test'")
    g.add_argument("--pose", default="", help="one-off variant tags (not saved), for pose exploration")
    g.add_argument("--neg", default="", help="one-off negative tags for this run")
    g.add_argument("--init", help="img2img: start from this image (keeps its composition)")
    g.add_argument("--init-strength", type=float, default=0.55, help="img2img strength, higher = more change")
    ip = sub.add_parser("inpaint", help="repaint a rectangle of an image (e.g. a broken hand)")
    ip.add_argument("char")
    ip.add_argument("variant", help="prompt variant to reuse for context")
    ip.add_argument("--image", required=True)
    ip.add_argument("--box", type=int, nargs=4, required=True, metavar=("X0", "Y0", "X1", "Y1"))
    ip.add_argument("--pad", type=int, default=0)
    ip.add_argument("--extra", default="", help="tags appended for the repaint, e.g. 'five fingers'")
    ip.add_argument("--neg", default="", help="extra negative tags, e.g. 'extra fingers'")
    ip.add_argument("--strength", type=float, default=1.0)
    ip.add_argument("--count", type=int, default=6)
    ip.add_argument("--delay", type=float, default=1.0)
    ip.add_argument("--allow-cost", action="store_true")
    s = sub.add_parser("sheet")
    s.add_argument("folder")
    s.add_argument("--cols", type=int, default=5)
    s.add_argument("--width", type=int, default=240)
    s.add_argument("--last", type=int, default=0)
    r = sub.add_parser("readmeta")
    r.add_argument("png")
    args = ap.parse_args()
    {"status": cmd_status, "gen": cmd_gen, "inpaint": cmd_inpaint, "sheet": cmd_sheet, "readmeta": cmd_readmeta}[args.cmd](args)


if __name__ == "__main__":
    main()
