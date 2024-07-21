using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI; // AI, 내비게이션 시스템 관련 코드를 가져오기

// 좀비 AI 구현
public class Zombie : LivingEntity
{
    public LayerMask whatIsTarget; // 공격 대상 레이어

    private LivingEntity targetEntity; // 추적할 대상
    private NavMeshAgent navMeshAgent; // 경로계산 AI 에이전트

    public ParticleSystem hitEffect; // 피격시 재생할 파티클 효과
    public AudioClip deathSound; // 사망시 재생할 소리
    public AudioClip hitSound; // 피격시 재생할 소리

    private Animator zombieAnimator; // 애니메이터 컴포넌트
    private AudioSource zombieAudioPlayer; // 오디오 소스 컴포넌트
    private Renderer zombieRenderer; // 렌더러 컴포넌트

    public float damage = 20f; // 공격력
    public float timeBetAttack = 0.5f; // 공격 간격
    private float lastAttackTime; // 마지막 공격 시점


    // 추적할 대상이 존재하는지 알려주는 프로퍼티
    private bool hasTarget
    {
        get
        {
            // 추적할 대상이 존재하고, 대상이 사망하지 않았다면 true
            if (targetEntity != null && !targetEntity.dead)
            {
                return true;
            }

            // 그렇지 않다면 false
            return false;
        }
    }

    private void Awake()
    {
        // 게임 오브젝트로부터 사용할 컴포넌트들을 가져오기
        navMeshAgent = GetComponent<NavMeshAgent>();
        zombieAnimator = GetComponent<Animator>();
        zombieAudioPlayer = GetComponent<AudioSource>();

        // 렌더러 컴포넌트는 자식 게임 오브젝트에게 있으므로
        // GetComponentInChildren() 메서드를 사용
        zombieRenderer = GetComponentInChildren<Renderer>();
    }

    // 19.7 절에서 다룰 '좀비 생성기 포팅'에서 다룰 좀비 생성기는 좀비를 모든 클라이언트에 
    // 동일하게 생성하고, 생성한 좀비의 능력치를 설정합니다.
    // 생성한 좀비가 모든 클라이언트에서 동일한 능력치를 가지게 하려면 모든 클라이언트에서 
    // Setup() 메서드가 실행되어야 합니다. 따라서  Setup() 메서드는 [PunRPC] attribute로 선언되어야 합니다.
    // 본래 Zombie 스크립트의 기존 Setup() 메서드에서는 ZombieData 타입을 직접 받도록 되어 있습니다. 
    // ZombieData는 스크립터블 오브젝트 에셋으로서 좀비에 필요한 데이터 외에도 우니티 오브젝트로서 필수적인 
    // 다른 데이터도 포함되어 있습니다. 즉, ZombieData 에셋을 직접 다른 클라이언트에 전송하면 실질적인 데이터에 비해 
    // 보내야 할 패킷 크깃가 커집니다. 따라서 ZombieData를 통해 전달받던 체력, 공격력, 피부색을 
    // Setup()의 입력 파라메터로 직접 받도록 했습니다. 
    // 또한 19.2.4절 'LivingEntity 스크립트’에서 OnDamage() 메서드에 이미 [PunRPC] 속성을 선언했지만，
    // Zombie 스크립트에서 OnDamage()를 오버라이드하면서 [PunRPC] 속성이 해지되었기 때문에 
    // Zombie의 OnDamage() 메서드에서 [PunRPC] 속성을 다시 선언했습니다.

    // 좀비 AI의 초기 스펙을 결정하는 셋업 메서드
    [PunRPC]
    public void Setup(float newHealth, float newDamage,
        float newSpeed, Color skinColor)
    {
        // 체력 설정
        startingHealth = newHealth;
        health = newHealth;
        // 공격력 설정
        damage = newDamage;
        // 내비메쉬 에이전트의 이동 속도 설정
        navMeshAgent.speed = newSpeed;
        // 렌더러가 사용중인 머테리얼의 컬러를 변경, 외형 색이 변함
        zombieRenderer.material.color = skinColor;
    }


    // Zombie의 Start() 메서드는 UpdatePath() 코루틴을 실행하여 Zombie 게임 오브젝트에 추가된 내비메시 에이전트가 
    // 적을 찾고 경로를 계산하여. 이동하게 합니다.
    // 만약 모든 클라이언트에서 내비메시 에이전트가 독자적으로 동작하면 내비메시 에이전트가 계산한 경로가 클라이언트마다 
    // 조금씩 다를 수 있습니다. 즉，Zombie AI가 클라이언트마다 서로 다른 경로로 이동할 수 있습니다.
    // 따라서 Zombie 게임 오브젝트의 내비메시 에이전트의 경로 계산과 이동은 호스트에서만 실행 합니다. 
    // 변경된 Start() 메서드는 최상단에 다음과 같은 if 문을 추가하여 현재 코드를 실행 중인 클라이언트가 
    // 호스트가 아닌 경우에는 경로 계산을 시작하는 UpdatePath() 코루틴을 실행하지 못하도록 막습니다.
    // 즉，호스트가 아닌 다른 클라이언트들의 Zombie 게임 오브젝트는 경로를 스스로 계산하지 않고, 
    // 호스트의 Zombie 게임 요브젝트의 위치를 동기화해서 이동합니다.
    // Zombie 게임 오브젝트는 호스트에서 먼저 생성되고，다른 클라이언트에서 복제 생성되기 때문에 
    // 호스트의 Zombie 게임 오브젝트는 로컬이며，다른 클라이언트의 Zombie 게임 오브젝트는 리모트입니다.
    // 따라서 호스트의 Zombie 게임 오브젝트 위치를 다른 클라이언트의 Zombie 게임 오브젝트가 받아 적용하는 과정은 
    // Photon View 컴포넌트에 의해 자동으로 이루어집니다.

    private void Start()
    {
        // 호스트가 아니라면 AI의 추적 루틴을 실행하지 않음
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        // 게임 오브젝트 활성화와 동시에 AI의 추적 루틴 시작
        StartCoroutine(UpdatePath());
    }

    // 변경된 Update() 메서드 또한 if 문을 추가하여 클라이언트가 호스트가 아닌 경우에는 애니 메이션 파라미터를 
    // 갱신하는 처리를 실행하지 못하게 합니다. 물론 호스트에서만 좀비의 애니메이터 파라미터를 직접 갱신해도 
    // Photon Animator View 컴포넌트에 의해 동기화되어 클라이언트에서도 같은 애니메이션이 재생되기 때문에 문제없습니다 .

    private void Update()
    {
        // 호스트가 아니라면 애니메이션의 파라미터를 직접 갱신하지 않음
        // 호스트가 파라미터를 갱신하면 클라이언트들에게 자동으로 전달되기 때문.
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        // 추적 대상의 존재 여부에 따라 다른 애니메이션을 재생
        zombieAnimator.SetBool("HasTarget", hasTarget);
    }

    // 주기적으로 추적할 대상의 위치를 찾아 경로를 갱신
    private IEnumerator UpdatePath()
    {
        // 살아있는 동안 무한 루프
        while (!dead)
        {
            if (hasTarget)
            {
                // 추적 대상 존재 : 경로를 갱신하고 AI 이동을 계속 진행
                navMeshAgent.isStopped = false;
                navMeshAgent.SetDestination(targetEntity.transform.position);
            }
            else
            {
                // 추적 대상 없음 : AI 이동 중지
                navMeshAgent.isStopped = true;

                // 20 유닛의 반지름을 가진 가상의 구를 그렸을때, 구와 겹치는 모든 콜라이더를 가져옴
                // 단, targetLayers에 해당하는 레이어를 가진 콜라이더만 가져오도록 필터링
                Collider[] colliders =
                    Physics.OverlapSphere(transform.position, 20f, whatIsTarget);

                // 모든 콜라이더들을 순회하면서, 살아있는 플레이어를 찾기
                for (int i = 0; i < colliders.Length; i++)
                {
                    // 콜라이더로부터 LivingEntity 컴포넌트 가져오기
                    LivingEntity livingEntity = colliders[i].GetComponent<LivingEntity>();

                    // LivingEntity 컴포넌트가 존재하며, 해당 LivingEntity가 살아있다면,
                    if (livingEntity != null && !livingEntity.dead)
                    {
                        // 추적 대상을 해당 LivingEntity로 설정
                        targetEntity = livingEntity;

                        // for문 루프 즉시 정지
                        break;
                    }
                }
            }

            // 0.25초 주기로 처리 반복
            yield return new WaitForSeconds(0.25f);
        }
    }


    // 데미지를 입었을때 실행할 처리
    [PunRPC]
    public override void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        // 아직 사망하지 않은 경우에만 피격 효과 재생
        if (!dead)
        {
            // 공격 받은 지점과 방향으로 파티클 효과를 재생
            hitEffect.transform.position = hitPoint;
            hitEffect.transform.rotation = Quaternion.LookRotation(hitNormal);
            hitEffect.Play();

            // 피격 효과음 재생
            zombieAudioPlayer.PlayOneShot(hitSound);
        }

        // LivingEntity의 OnDamage()를 실행하여 데미지 적용
        base.OnDamage(damage, hitPoint, hitNormal);
    }

    // 사망 처리
    public override void Die()
    {
        // LivingEntity의 Die()를 실행하여 기본 사망 처리 실행
        base.Die();

        // 다른 AI들을 방해하지 않도록 자신의 모든 콜라이더들을 비활성화
        Collider[] zombieColliders = GetComponents<Collider>();
        for (int i = 0; i < zombieColliders.Length; i++)
        {
            zombieColliders[i].enabled = false;
        }

        // AI 추적을 중지하고 내비메쉬 컴포넌트를 비활성화
        navMeshAgent.isStopped = true;
        navMeshAgent.enabled = false;

        // 사망 애니메이션 재생
        zombieAnimator.SetTrigger("Die");

        // 사망 효과음 재생
        zombieAudioPlayer.PlayOneShot(deathSound);
    }

    // Zombie 스크립트의 OnTriggerStay() 메서드는 트리거 콜라이더를 사용해 감지된 상대방 게임 오브젝트가 추적 대상인 경우 
    // 해당 게임 오브젝트를 공격하는 처리를 구현합니다. 변경된 OnTriggerStay() 메서드는 최상단에 if 문을 추가하여 
    // 클라이언트가 호스트가 아닌 경우에는 공격을 실행하지 못하게 막습니다. 즉，Zombie의 공격은 호스트에서만 이루어집니다.
    // 단，공격을 받는 LivingEntity 타입은 19.2.4절 ‘LivingEntity 스크립트’에서 살펴봤듯이 공격당한 결과를 
    // 다른 클라이언트에 RPC로 전파합니다. 따라서 좀비가 플레이어 캐릭터를 공격한 결과는 호스트가 아닌 다른 클라이언트에도 
    // 무사히 적용됩니다.

    private void OnTriggerStay(Collider other)
    {
        // 호스트가 아니라면 공격 실행 불가
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        // 자신이 사망하지 않았으며,
        // 최근 공격 시점에서 timeBetAttack 이상 시간이 지났다면 공격 가능
        if (!dead && Time.time >= lastAttackTime + timeBetAttack)
        {
            // 상대방으로부터 LivingEntity 타입을 가져오기 시도
            LivingEntity attackTarget
                = other.GetComponent<LivingEntity>();

            // 상대방의 LivingEntity가 자신의 추적 대상이라면 공격 실행
            if (attackTarget != null && attackTarget == targetEntity)
            {
                // 최근 공격 시간을 갱신
                lastAttackTime = Time.time;

                // 상대방의 피격 위치와 피격 방향을 근삿값으로 계산
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitNormal = transform.position - other.transform.position;

                // 공격 실행
                attackTarget.OnDamage(damage, hitPoint, hitNormal);
            }
        }
    }
}