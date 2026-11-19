using RingsideHearts.Battle.Core;
using UnityEngine;

namespace RingsideHearts.Battle.View
{
    /// <summary>
    /// Every tunable battle number in one asset (Assets/Resources/Data/GameVariables.asset).
    /// Read again at each shot, so edits made in the Inspector during play apply to the next shot
    /// and are kept after leaving play mode. The field list is generated from one table together
    /// with <see cref="GameVariableKeys"/> (field → key → default).
    /// </summary>
    [CreateAssetMenu(menuName = "Ringside Hearts/Game Variables", fileName = "GameVariables")]
    public sealed class GameVariables : ScriptableObject
    {
        public const string ResourcePath = "Data/GameVariables";

        [Header("굴러가는 느낌")]
        [Tooltip("일정 감속 (/초²). 올리면 끝부분이 덜 미끄러짐")]
        [Range(0.0f, 10.0f)] public float friction = 1.5f;
        [Tooltip("속도 비례 감속 (/초). 올리면 빠른 구간이 빨리 줄어듦")]
        [Range(0.0f, 3.0f)] public float drag = 0.6f;
        [Tooltip("최대로 당겼을 때 속도")]
        [Range(5.0f, 60.0f)] public float shotMaxSpeed = 25.0f;

        [Header("로프")]
        [Tooltip("로프에 튕긴 뒤 남는 속도 비율")]
        [Range(0.0f, 1.2f)] public float ropeRestitution = 0.85f;
        [Tooltip("로프 탄성. 낮추면 더 깊고 느리게 휨")]
        [Range(50.0f, 3000.0f)] public float ropeStiffness = 780.0f;
        [Tooltip("로프가 최대로 밀려나는 거리 (링 단위)")]
        [Range(0.0f, 2.0f)] public float ropeMaxStretch = 1.0f;

        [Header("유닛끼리 부딪힘")]
        [Tooltip("부딪친 쪽이 튕겨 나가며 남는 속도 비율")]
        [Range(0.0f, 1.2f)] public float attackerRestitution = 0.55f;
        [Tooltip("맞은 쪽이 받는 속도 = 부딪친 쪽 속도 × 이 값")]
        [Range(0.0f, 1.5f)] public float knockback = 0.7f;
        [Tooltip("날아간 상대가 다른 상대에게 부딪히면 그 상대가 받는 위력 비율")]
        [Range(0.0f, 2.0f)] public float bowling = 0.5f;

        [Header("히트스톱 (유닛끼리 부딪히면 화면이 잠깐 멈춤)")]
        [Tooltip("기본 멈춤 시간 (초). 0이면 끔")]
        [Range(0.0f, 0.3f)] public float hitStopSeconds = 0.06f;
        [Tooltip("부딪힌 속도 1당 더 멈추는 시간 (초)")]
        [Range(0.0f, 0.02f)] public float hitStopPerSpeed = 0.003f;
        [Tooltip("최대 멈춤 시간 (초)")]
        [Range(0.0f, 0.5f)] public float hitStopMaxSeconds = 0.15f;

        [Header("로프 히트슬로우 (세게 박힐수록 더 느리게, 더 오래)")]
        [Tooltip("로프에 들어간 속도가 이보다 약하면 슬로우 없음")]
        [Range(0.0f, 30.0f)] public float ropeSlowMinSpeed = 4.0f;
        [Tooltip("이 속도 이상이면 최대 슬로우")]
        [Range(0.0f, 40.0f)] public float ropeSlowFullSpeed = 20.0f;
        [Tooltip("최대 슬로우일 때 재생 속도 배율. 0이면 완전 정지, 1이면 끔")]
        [Range(0.0f, 1.0f)] public float ropeSlowScale = 0.2f;
        [Tooltip("최대 슬로우에서 원래 속도로 돌아오는 시간 (초)")]
        [Range(0.0f, 1.5f)] public float ropeSlowSeconds = 0.3f;

        [Header("재생")]
        [Tooltip("전체 재생 배속")]
        [Range(0.1f, 3.0f)] public float playbackSpeed = 1.0f;

        [Header("피해")]
        [Tooltip("타격 피해 = 파워 × 이 값 × 여러 배율")]
        [Range(0.1f, 10.0f)] public float damageScale = 0.75f;
        [Tooltip("바디(누적 피해) = 피해 × 이 값")]
        [Range(0.0f, 2.0f)] public float bodyRatio = 0.4f;
        [Tooltip("내가 로프에 한 번 튕길 때마다 다음 타격 위력 +이 비율")]
        [Range(0.0f, 1.0f)] public float ropeBonusPerBounce = 0.15f;
        [Tooltip("반동 보너스를 셀 최대 횟수")]
        [Range(0, 10)] public int ropeBonusMax = 3;
        [Tooltip("로프에 튕겨 돌아오는 상대를 맞히면 위력 배율")]
        [Range(1.0f, 3.0f)] public float lariat = 1.5f;
        [Tooltip("속성 유리 배율")]
        [Range(1.0f, 2.0f)] public float attrAdvantage = 1.3f;
        [Tooltip("속성 불리 배율")]
        [Range(0.3f, 1.0f)] public float attrDisadvantage = 0.8f;
        [Tooltip("열기 0~100에서 위력 −이 값 ~ +이 값")]
        [Range(0.0f, 0.5f)] public float heatDamageSpan = 0.1f;
        [Tooltip("열기 의존 캐릭터의 위력 변화 폭")]
        [Range(0.0f, 0.8f)] public float heatDamageSpanDependent = 0.3f;
        [Tooltip("코너에서 뛰어내린 첫 타격 배율")]
        [Range(1.0f, 3.0f)] public float diveMul = 1.6f;
        [Tooltip("합동 필살기 배율")]
        [Range(1.0f, 8.0f)] public float jointMul = 4.0f;

        [Header("타입")]
        [Tooltip("스트라이커: 로프를 거치지 않은 타격 배율")]
        [Range(1.0f, 3.0f)] public float strikerDirect = 1.4f;
        [Tooltip("스트라이커: 맞은 상대가 밀려나는 배율")]
        [Range(1.0f, 4.0f)] public float strikerKnock = 1.6f;
        [Tooltip("스트라이커 필살기: 밀어내기 배율")]
        [Range(1.0f, 5.0f)] public float strikerKnockSpecial = 2.4f;
        [Tooltip("쇼메이커: 반동 1회당 위력 보너스")]
        [Range(0.0f, 1.0f)] public float showmanBonus = 0.3f;
        [Tooltip("쇼메이커 필살기: 반동 보너스 최대 횟수")]
        [Range(1, 10)] public int showmanMaxSpecial = 5;
        [Tooltip("그래플러: 붙잡기 타격 배율")]
        [Range(1.0f, 3.0f)] public float grabMul = 1.2f;
        [Tooltip("스탬피더: 뚫고 지나간 뒤 남는 속도")]
        [Range(0.0f, 1.0f)] public float stampKeep = 0.9f;
        [Tooltip("스탬피더: 뚫린 상대가 밀려나는 배율")]
        [Range(0.0f, 1.5f)] public float stampKnock = 0.6f;
        [Tooltip("하이플라이어: 로프에서 날아가는 거리")]
        [Range(0.0f, 8.0f)] public float flyDistance = 3.5f;
        [Tooltip("하이플라이어: 착지 타격 반경")]
        [Range(0.0f, 5.0f)] public float landRadius = 2.0f;
        [Tooltip("하이플라이어: 착지 타격 배율")]
        [Range(0.5f, 3.0f)] public float landMul = 1.3f;
        [Tooltip("그래플러: 잡은 상대를 링 중앙으로 던지는 속도")]
        [Range(4.0f, 40.0f)] public float throwSpeed = 16.0f;
        [Tooltip("로프 타이밍 필살기: 로프를 박찰 때 속도 배율")]
        [Range(1.0f, 2.0f)] public float specialRopeBoost = 1.25f;

        [Header("다운")]
        [Tooltip("다운 중 턴마다 회복 = 최대 스태미나 × (이 값 + 근성 × 아래 값)")]
        [Range(0.0f, 0.5f)] public float downRecoverBase = 0.15f;
        [Tooltip("근성 1당 더 회복하는 비율")]
        [Range(0.0f, 0.002f)] public float downRecoverPerGuts = 0.0003f;
        [Tooltip("최대 스태미나의 이 비율까지 차면 일어남")]
        [Range(0.05f, 1.0f)] public float downWakeRatio = 0.3f;
        [Tooltip("피날레를 버텨 낸 쪽이 회복해 일어나는 비율")]
        [Range(0.0f, 1.0f)] public float finaleFailWakeRatio = 0.3f;

        [Header("태그와 에이프런")]
        [Tooltip("태그로 나간 선수가 다시 들어올 수 없는 턴 (턴 끝마다 −1)")]
        [Range(0, 5)] public int tagLockTurns = 2;
        [Tooltip("대기 선수가 로프 밖에 서는 거리")]
        [Range(0.5f, 1.5f)] public float apronOffset = 0.9f;
        [Tooltip("대기 선수 크기 (닿는 판정)")]
        [Range(0.2f, 1.0f)] public float apronRadius = 0.56f;
        [Tooltip("대기 중 턴마다 회복하는 비율")]
        [Range(0.0f, 0.3f)] public float apronRecover = 0.05f;
        [Tooltip("에이프런에서 떨어진 선수가 돌아오는 데 걸리는 턴")]
        [Range(0, 5)] public int apronFallTurns = 2;
        [Tooltip("세컨드가 짝에게 더해 주는 능력치 비율")]
        [Range(0.0f, 1.0f)] public float secondShare = 0.2f;

        [Header("어시스트 (내 샷이 에이프런의 동료에게 닿으면)")]
        [Tooltip("라리아트 어시스트: 직선 폭의 절반")]
        [Range(0.0f, 3.0f)] public float assistLariatWidth = 0.7f;
        [Tooltip("라리아트 어시스트 위력 배율")]
        [Range(0.0f, 3.0f)] public float assistLariatMul = 1.0f;
        [Tooltip("스톰프 어시스트 반경")]
        [Range(0.0f, 6.0f)] public float assistStompRadius = 2.5f;
        [Tooltip("스톰프 어시스트 위력 배율")]
        [Range(0.0f, 3.0f)] public float assistStompMul = 0.7f;
        [Tooltip("함성 어시스트 열기")]
        [Range(0.0f, 50.0f)] public float assistCheerHeat = 10.0f;
        [Tooltip("아이싱 어시스트: 쏜 선수 스태미나 회복 비율")]
        [Range(0.0f, 0.5f)] public float assistIcing = 0.08f;

        [Header("링 위의 것들")]
        [Tooltip("코너에서 이 거리 안에 멈추면 다음 샷 다이브")]
        [Range(0.8f, 4.0f)] public float cornerRadius = 2.0f;
        [Tooltip("심판 크기")]
        [Range(0.2f, 1.0f)] public float refRadius = 0.45f;
        [Tooltip("맞은 심판이 쓰러져 있는 턴")]
        [Range(0, 5)] public int refDownTurns = 2;
        [Tooltip("심판이 턴마다 걷는 최대 거리")]
        [Range(0.0f, 6.0f)] public float refWalk = 2.5f;
        [Tooltip("케이지 철망 반발")]
        [Range(0.5f, 1.3f)] public float cageRestitution = 1.0f;
        [Tooltip("케이지 철망이 밀리는 거리")]
        [Range(0.0f, 1.0f)] public float cageStretch = 0.25f;
        [Tooltip("맞고 날아가 철망에 부딪힌 피해")]
        [Range(0.0f, 500.0f)] public float cageDamage = 120.0f;
        [Tooltip("테이블 크기")]
        [Range(0.3f, 2.0f)] public float tableRadius = 0.9f;
        [Tooltip("의자 크기")]
        [Range(0.2f, 1.0f)] public float chairRadius = 0.45f;
        [Tooltip("맞고 날아가 테이블에 부딪힌 피해")]
        [Range(0.0f, 800.0f)] public float tableDamage = 250.0f;
        [Tooltip("맞고 날아가 의자에 부딪힌 피해")]
        [Range(0.0f, 800.0f)] public float chairDamage = 180.0f;
        [Tooltip("먼저 부딪혀 집은 흉기로 다음 타격 배율")]
        [Range(1.0f, 3.0f)] public float weaponMul = 1.5f;

        [Header("관중 열기")]
        [Tooltip("시작 열기")]
        [Range(0.0f, 100.0f)] public float heatStart = 40.0f;
        [Tooltip("턴마다 줄어드는 열기")]
        [Range(0.0f, 30.0f)] public float heatDecay = 8.0f;
        [Tooltip("아무도 못 맞힌 턴에 더 줄어드는 열기")]
        [Range(0.0f, 30.0f)] public float heatNoHit = 10.0f;
        [Tooltip("반동 타격의 반동 1회당 열기")]
        [Range(0.0f, 20.0f)] public float heatPerBounceHit = 2.0f;
        [Tooltip("쇼메이커가 로프에 튕길 때마다 열기")]
        [Range(0.0f, 20.0f)] public float heatShowmanBounce = 2.0f;
        [Tooltip("공중기 착지 열기")]
        [Range(0.0f, 20.0f)] public float heatAerial = 3.0f;
        [Tooltip("라리아트 열기")]
        [Range(0.0f, 30.0f)] public float heatLariat = 6.0f;
        [Tooltip("다운을 만들면 열기")]
        [Range(0.0f, 30.0f)] public float heatDown = 6.0f;
        [Tooltip("체인 한 칸 열기")]
        [Range(0.0f, 30.0f)] public float heatChain = 3.0f;
        [Tooltip("피날레를 아깝게 놓치면 공격 측 열기 (니어폴)")]
        [Range(0.0f, 30.0f)] public float heatFinaleFail = 10.0f;
        [Tooltip("힐 어필 열기 배율 (대신 심판 경고)")]
        [Range(1.0f, 3.0f)] public float appealHeelMul = 1.5f;
        [Tooltip("같은 선수가 연달아 어필하면 배율")]
        [Range(0.0f, 1.0f)] public float appealRepeatMul = 0.5f;
        [Tooltip("어필한 선수의 필살기 카운트가 이만큼 줄어듦")]
        [Range(0, 5)] public int appealSpecialCharge = 1;
        [Tooltip("심판 경고가 이만큼 쌓이면 힘겨루기 불리")]
        [Range(1, 10)] public int warningsPenaltyAt = 3;
        [Tooltip("경고 누적 시 힘겨루기 시작 불리 양")]
        [Range(0.0f, 40.0f)] public float warningsPenalty = 10.0f;

        [Header("피날레 공통")]
        [Tooltip("열기 0일 때 피날레 힘 배율")]
        [Range(0.0f, 2.0f)] public float heatMulMin = 0.8f;
        [Tooltip("열기 100일 때 피날레 힘 배율")]
        [Range(0.0f, 3.0f)] public float heatMulMax = 1.2f;
        [Tooltip("바디가 0이어도 남는 방어 힘 비율")]
        [Range(0.0f, 1.0f)] public float finaleBodyFloor = 0.1f;
        [Tooltip("필살기로 다운된 상대의 방어 힘 배율")]
        [Range(0.0f, 1.0f)] public float finaleSpecialResist = 0.7f;
        [Tooltip("힘겨루기 시작 전 장면과 조작을 보여 주는 시간 (입력 안 받음, 게이지 고정)")]
        [Range(0.0f, 5.0f)] public float finaleIntro = 1.8f;
        [Tooltip("양손 '동시' 판정 시간 (초)")]
        [Range(0.03f, 0.5f)] public float inputBothWindow = 0.15f;
        [Tooltip("페이스 세컨드 응원: 입력 배율")]
        [Range(1.0f, 3.0f)] public float interveneFaceMul = 1.5f;
        [Tooltip("페이스 세컨드 응원 시간")]
        [Range(0.0f, 8.0f)] public float interveneFaceSeconds = 3.0f;
        [Tooltip("힐 세컨드 난입: 상대가 멈추는 시간")]
        [Range(0.0f, 8.0f)] public float interveneHeelSeconds = 2.0f;
        [Tooltip("AI가 불리할 때(진행 이 비율 이하) 개입")]
        [Range(0.0f, 1.0f)] public float interveneAiBelow = 0.25f;

        [Header("핀폴")]
        [Tooltip("누르기 게이지 시작값")]
        [Range(0.0f, 100.0f)] public float pinStart = 60.0f;
        [Tooltip("공격 연타 한 번 (× 공격 힘)")]
        [Range(0.0f, 30.0f)] public float pinTap = 6.0f;
        [Tooltip("방어 측이 초당 밀어 올리는 양 (× 방어 힘)")]
        [Range(0.0f, 80.0f)] public float pinDefend = 140.0f;
        [Tooltip("게이지가 이 값 이상인 동안 심판 카운트 진행")]
        [Range(1.0f, 99.0f)] public float pinThreshold = 50.0f;
        [Tooltip("카운트 하나 걸리는 시간")]
        [Range(0.2f, 3.0f)] public float pinCountSeconds = 0.8f;
        [Tooltip("이 카운트면 승리")]
        [Range(1, 5)] public int pinCounts = 3;
        [Tooltip("심판이 카운트를 시작하기까지 기본 시간")]
        [Range(0.0f, 2.0f)] public float pinRefDelayBase = 0.3f;
        [Tooltip("심판과 떨어진 거리 1당 더 늦어지는 시간")]
        [Range(0.0f, 1.0f)] public float pinRefDelayPerUnit = 0.15f;
        [Tooltip("킥아웃 판정 폭 (초, × 방어 힘/공격 힘)")]
        [Range(0.01f, 0.5f)] public float pinWindow = 0.1f;
        [Tooltip("킥아웃 판정 폭 최소")]
        [Range(0.01f, 0.5f)] public float pinWindowMin = 0.015f;
        [Tooltip("킥아웃 판정 폭 최대")]
        [Range(0.01f, 1.0f)] public float pinWindowMax = 0.15f;

        [Header("서브미션")]
        [Tooltip("조이기 게이지 시작값")]
        [Range(0.0f, 100.0f)] public float subStart = 20.0f;
        [Tooltip("조이기 연타 한 번 (× 공격 힘)")]
        [Range(0.0f, 30.0f)] public float subTap = 5.0f;
        [Tooltip("방어 측이 초당 버티는 양 (× 방어 힘)")]
        [Range(0.0f, 80.0f)] public float subResist = 85.0f;
        [Tooltip("기어가기 연타 한 번 (× 방어 힘)")]
        [Range(0.0f, 30.0f)] public float subCrawl = 5.0f;
        [Tooltip("로프 근처에서 걸리면 기어가기 시작값")]
        [Range(0.0f, 100.0f)] public float subNearRopeStart = 60.0f;
        [Tooltip("제한 시간 (지나면 탈출)")]
        [Range(1.0f, 15.0f)] public float subSeconds = 6.0f;

        [Header("링아웃")]
        [Tooltip("상대를 로프까지 밀어붙인 거리 1당 넘김 게이지 시작값이 줄어듦 (중앙에서 걸면 불리)")]
        [Range(0.0f, 20.0f)] public float ringoutPushPenalty = 5.0f;
        [Tooltip("넘김 게이지 시작값")]
        [Range(0.0f, 100.0f)] public float ringoutStart = 50.0f;
        [Tooltip("흔들림 한 번 주기 (초)")]
        [Range(0.4f, 3.0f)] public float ringoutPeriod = 1.2f;
        [Tooltip("끝점 타이밍 판정 폭 (초)")]
        [Range(0.02f, 0.5f)] public float ringoutWindow = 0.24f;
        [Tooltip("공격 측 밀어 넘기기 한 번 (× 공격 힘)")]
        [Range(0.0f, 50.0f)] public float ringoutPush = 14.0f;
        [Tooltip("방어 측 끌어당기기 한 번 (× 방어 힘)")]
        [Range(0.0f, 50.0f)] public float ringoutPull = 30.0f;
        [Tooltip("제한 시간 (지나면 복귀)")]
        [Range(1.0f, 15.0f)] public float ringoutSeconds = 7.0f;

        [Header("상대 AI")]
        [Tooltip("후보 방향 수")]
        [Range(4, 72)] public int aiRushDirections = 24;
        [Tooltip("후보 세기 1 (최대 속도 대비)")]
        [Range(0.1f, 1.0f)] public float aiRushPower1 = 0.4f;
        [Tooltip("후보 세기 2")]
        [Range(0.1f, 1.0f)] public float aiRushPower2 = 0.7f;
        [Tooltip("후보 세기 3")]
        [Range(0.1f, 1.0f)] public float aiRushPower3 = 1.0f;
        [Tooltip("후보 점수: 다운시키면")]
        [Range(0.0f, 2000.0f)] public float aiDownBonus = 500.0f;
        [Tooltip("후보 점수: 라리아트 1회")]
        [Range(0.0f, 1000.0f)] public float aiLariatBonus = 100.0f;
        [Tooltip("후보 점수: 상대 대기 선수 떨어뜨리기")]
        [Range(0.0f, 1000.0f)] public float aiKnockoffBonus = 80.0f;
        [Tooltip("후보 점수: 코너에 멈추기")]
        [Range(0.0f, 1000.0f)] public float aiCornerBonus = 60.0f;
        [Tooltip("후보 점수: 체인 한 칸")]
        [Range(0.0f, 1000.0f)] public float aiChainBonus = 60.0f;
        [Tooltip("다운된 상대가 링 중심에서 링 폭 × 이 비율 안이면 서브미션")]
        [Range(0.0f, 0.5f)] public float aiCenterRatio = 0.25f;
        [Tooltip("열기가 이보다 낮으면 어필을 고려")]
        [Range(0.0f, 100.0f)] public float aiAppealBelow = 30.0f;
        [Tooltip("어필을 고려할 때 실제로 할 확률")]
        [Range(0.0f, 1.0f)] public float aiAppealChance = 0.4f;
        [Tooltip("링 안 선수 스태미나가 이보다 낮으면 태그 고려")]
        [Range(0.0f, 1.0f)] public float aiTagLow = 0.25f;
        [Tooltip("교대할 동료의 스태미나가 이 이상이어야 함")]
        [Range(0.0f, 1.0f)] public float aiTagHealthy = 0.6f;

        [Header("구조 (링 크기, 계산 간격 등 — 거의 안 바꿈)")]
        [Tooltip("로프 안쪽 한 변 (링 단위)")]
        [Range(5.0f, 20.0f)] public float ringSize = 10.0f;
        [Tooltip("유닛 반지름 (링 단위)")]
        [Range(0.3f, 1.5f)] public float unitRadius = 0.7f;
        [Tooltip("이보다 짧게 당기면 취소 (최대 당김 대비)")]
        [Range(0.0f, 0.5f)] public float shotMinPullRatio = 0.15f;
        [Tooltip("계산 간격 (초). 1/120")]
        [Range(0.002f, 0.05f)] public float simDt = 0.0083333f;
        [Tooltip("샷 하나의 최대 계산 시간 (초)")]
        [Range(1.0f, 60.0f)] public float simMaxSeconds = 20.0f;
        [Tooltip("로프 근처 기준 (유닛 지름 배수)")]
        [Range(0.0f, 5.0f)] public float nearRopeDiameters = 1.5f;
        [Tooltip("에이프런 띠 폭 (링 칸 폭 대비)")]
        [Range(0.0f, 0.3f)] public float apronRatio = 0.12f;
        [Tooltip("최대 당김 거리 (캔버스 단위)")]
        [Range(100.0f, 1000.0f)] public float maxPullUnits = 380.0f;
        [Tooltip("시작 위치: 링 아래(내 팀)/위(상대)에서 이 비율 지점")]
        [Range(0.1f, 0.45f)] public float startRatio = 0.25f;

        public static GameVariables Load()
        {
            var asset = Resources.Load<GameVariables>(ResourcePath);
            if (asset == null) throw new System.IO.FileNotFoundException("Resources/" + ResourcePath + ".asset is missing");
            return asset;
        }

        /// <summary>The values under the keys the battle core reads (field → key table in Core).</summary>
        public Tuning ToTuning()
        {
            var t = new Tuning();
            string[,] map = GameVariableKeys.Map;
            for (int i = 0; i < map.GetLength(0); i++)
            {
                System.Reflection.FieldInfo field = typeof(GameVariables).GetField(map[i, 0]);
                if (field == null) throw new System.MissingFieldException(nameof(GameVariables), map[i, 0]);
                t.Set(map[i, 1], System.Convert.ToSingle(field.GetValue(this)));
            }
            return t;
        }
    }
}
