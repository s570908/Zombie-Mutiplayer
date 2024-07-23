using Photon.Pun;
using UnityEngine;
using UnityEngine.UI; // UI 관련 코드

/*************

LivingEntity에서 RestoreHealth ()와 OnDamage () 메서드는 [PunRPC] 속성이 선언되어 있었습니다.
오버라이드하는 측에서도 원본 메서드와 동일하게 [PunRPC] 속성을 선언해야 정상적으로 RPC를 통해 원격 
실행할 수 있습니다. 따라서 PlayerHealth 스크립트의 RestoreHealth ()와 OnDamage () 에도 
동일한 [PunRPC] 속성을 선언했습니다.

어떤 클라이언트에서 PlayerHealth 스크립트의 OnDamage ()가 실행되었다고 가정했을 때 클라이언트가 호스트가 
맞든 아니든 효과음을 재생하고 체력 슬라이더를 갱신하는 부분은 모두 제대로 실행됩니다. 
단，체력을 변경하는 부분은 호스트에서만 실행됩니다.

PlayerHealth의 OnDamage () 메서드에서 base.OnDamage (damage, hitPoint, hitDirection); 은 
Living티Ttity의 OnDamage ()를 실행하는 겁니다. 그런데 LivingEntity의 OnDamage ()에서 체력을 
변경하는 처리는 호스트에서만 실행됩니다. 즉，모든 클라이언트에서 PlayerHealth의 OnDamage ()가 동시에 
실행된다고 가정했을 때 실제 대미지 적용은 호스트에서만 실행됩니다. 나머지 클라이언트는 대미지를 입었을 때 
겉으로 보이는 효과만 재생하게 됩니다. PlayerHealth의 RestoreHealth () 메서드도 마찬가지입니다.

Die() 메서드
===========
기존 Die() 메서드에 Invoke ("Respawn", 5f);를 추가했습니다. Invoke () 메서드는특정 메서드를 
지연 실행하는 메서드입니다. Invoke () 메서드는 지연 실행할 메서드의 이름과 지연시간을 입력받습니다.
따라서 Die () 메서드가 실행되고 사망 후 5초 뒤에 Respawn () 메서드가 실행됩니다.

Respawn() 메서드
===============
새로 추가한 Respawn () 메서드는 사망한 플레이어 캐릭터를 부활시켜 재배치 (리스폰)하는 메서드입니다.
부활 처리는 단순히 게임 오브젝트를 끄고 다시 켜는 간단한 방식으로 구현합니다.

gameObject.SetActive(faIse); 
gameObject.SetActive(true);

이것이 가능한 이유는 5부에서 플레이어 캐릭터 관련 스크립트를 작성할 때 수치 초기화를 Awake () 또는 Start () 
대신 OnEnable () 메서드에 몰아뒀기 때문입니다.
예를 들어 LivingEntity에서 체력을 최대 체력값으로 초기화하는 코드는 OnEnable () 메서드
에 있습니다. 즉，게임 오브젝트를 끄고 다시 켜면 PlayerHealth, LivingEntity 등의 스크립트의 
OnEnable () 메서드가 자동 실행되고，체력 등 각종 수치가 기본값으로 다시 리셋됩니다.


게임 오브젝트를 끄고 다시 켜는 처리 앞에는 자신의 게임 오브젝트 위치를 임의 위치로 옮기는 처리가 있습니다. 
랜덤 위치는 반지름 5의 구 내부에서 임의 위치를 찾고, 높이 y 값을 0으로 변경하여 구현했습니다.

if (photonView.IsMine) {
    Vector3 randomSpawnPos = Random.insideUnitSphere * 5f; randomSpawnPos.y = 0f;
    transform.position = randomSpawnPos; 
}

그런데 위치를 랜덤 지정하는 처리는 if (photonView.IsMine)에 의해 현재 게임 오브젝트가 로컬인 경우에만 
실행됩니다.

클라이언트 A, B, C가 있고，플레이어 캐릭터 a가 사망했다고 가정합시다. 클라이언트 A 입장에서 a는 로컬 플레이어，
B와 C 입장에서는 리모트 플레이어입니다. 플레이어 캐릭터 a에서의 Respawn () 메서드는 모든 클라이언트 
A, B, C에서 실행됩니다. 클라이언트 B, C에서 실행된 a의 Respawn () 메서드는 앞의 if 문 블록이 실행되지 못하고，
게임 오브젝트를 끄고 켜는 처리만 실행합니다. 클라이언트 A에서의 a는 앞의 if 문 블록 조건을 만족하기 때문에 
캐릭터 a의 위치를 랜덤 위치로 변경하는 처리까지 함께 실행됩니다. 즉，리스폰 과정에서 실제 위치는 클라이언트 
A에서만 변경됩니다. 단，Player Character 게임 오브젝트에 Photon View 컴포넌트와 
Photon Transform View 컴포넌트가 추가되어 있기 때문에 클라이언트 A의 로컬 게임 오브젝트 a는 
클라이언트 B와 C의 리모트 게임 오브젝트 a와 위치를 동기화합니다. 따라서 리모트 플레이어인 경우 리스폰 과정에서 
직접 위치를 변경할 필요가 없습니다.

OnTriggerEnter() 메서드
=======================

기존 OnTriggerEnter () 메서드는 충돌한 아이템을 감지하고 사용하는 처리를 구현했습니다.
변경된 OnTriggerEnter ()는 기존 아이템 사용 처리 item.Use (gameObject);를 if 문으로 감싸서 
호스트에서만 실행합니다.

if (PhotonNetwork.IsMasterClient)
{
    item .Use (gameObject);
} 

playerAudioPlayer.PlayOneShot(itemPickupClip);

즉，아이템을 먹는 효과음은 모든 클라이언트에서 실행되지만，아이템 효과를 적용하는 부분은 호스트에서만 실행됩니다.
따라서 호스트가 아이템의 사용 결과를 다른 클라이언트로 전파할 수 있도록 아이템 스크립트를 수정해야 합니다. 
해당 수정사항은 19.5절 "네트워크 아이템’에서 구현합니다.

지금까지 Player Character 게임 요브젝트에 추가된 기존 스크립트의 변경 사항을 모두 살펴 봤습니다.

**************/

// 플레이어 캐릭터의 생명체로서의 동작을 담당
public class PlayerHealth : LivingEntity
{
    public Slider healthSlider; // 체력을 표시할 UI 슬라이더

    public AudioClip deathClip; // 사망 소리
    public AudioClip hitClip; // 피격 소리
    public AudioClip itemPickupClip; // 아이템 습득 소리

    private AudioSource playerAudioPlayer; // 플레이어 소리 재생기
    private Animator playerAnimator; // 플레이어의 애니메이터

    private PlayerMovement playerMovement; // 플레이어 움직임 컴포넌트
    private PlayerShooter playerShooter; // 플레이어 슈터 컴포넌트

    private void Awake()
    {
        // 사용할 컴포넌트를 가져오기
        playerAnimator = GetComponent<Animator>();
        playerAudioPlayer = GetComponent<AudioSource>();

        playerMovement = GetComponent<PlayerMovement>();
        playerShooter = GetComponent<PlayerShooter>();
    }

    protected override void OnEnable()
    {
        // LivingEntity의 OnEnable() 실행 (상태 초기화)
        base.OnEnable();

        // 체력 슬라이더 활성화
        healthSlider.gameObject.SetActive(true);
        // 체력 슬라이더의 최대값을 기본 체력값으로 변경
        healthSlider.maxValue = startingHealth;
        // 체력 슬라이더의 값을 현재 체력값으로 변경
        healthSlider.value = health;

        // 플레이어 조작을 받는 컴포넌트들 활성화
        playerMovement.enabled = true;
        playerShooter.enabled = true;
    }

    // 체력 회복
    [PunRPC]
    public override void RestoreHealth(float newHealth)
    {
        // LivingEntity의 RestoreHealth() 실행 (체력 증가)
        base.RestoreHealth(newHealth);
        // 체력 갱신
        healthSlider.value = health;
    }


    // 데미지 처리
    [PunRPC]
    public override void OnDamage(float damage, Vector3 hitPoint,
        Vector3 hitDirection)
    {
        if (!dead)
        {
            // 사망하지 않은 경우에만 효과음을 재생
            playerAudioPlayer.PlayOneShot(hitClip);
        }

        // LivingEntity의 OnDamage() 실행(데미지 적용)
        base.OnDamage(damage, hitPoint, hitDirection);
        // 갱신된 체력을 체력 슬라이더에 반영
        healthSlider.value = health;
    }

    public override void Die()
    {
        // LivingEntity의 Die() 실행(사망 적용)
        base.Die();

        // 체력 슬라이더 비활성화
        healthSlider.gameObject.SetActive(false);

        // 사망음 재생
        playerAudioPlayer.PlayOneShot(deathClip);

        // 애니메이터의 Die 트리거를 발동시켜 사망 애니메이션 재생
        playerAnimator.SetTrigger("Die");

        // 플레이어 조작을 받는 컴포넌트들 비활성화
        playerMovement.enabled = false;
        playerShooter.enabled = false;

        // 5초 뒤에 리스폰
        Invoke("Respawn", 5f);
    }

    private void OnTriggerEnter(Collider other)
    {
        // 아이템과 충돌한 경우 해당 아이템을 사용하는 처리
        // 사망하지 않은 경우에만 아이템 사용가능
        if (!dead)
        {
            // 충돌한 상대방으로 부터 Item 컴포넌트를 가져오기 시도
            IItem item = other.GetComponent<IItem>();

            // 충돌한 상대방으로부터 Item 컴포넌트가 가져오는데 성공했다면
            if (item != null)
            {
                // 호스트만 아이템 직접 사용 가능
                // 호스트에서는 아이템을 사용 후, 사용된 아이템의 효과를 모든 클라이언트들에게 동기화시킴
                if (PhotonNetwork.IsMasterClient)
                {
                    // Use 메서드를 실행하여 아이템 사용
                    item.Use(gameObject);
                }

                // 아이템 습득 소리 재생
                playerAudioPlayer.PlayOneShot(itemPickupClip);
            }
        }
    }

    // 부활 처리
    public void Respawn()
    {
        // 로컬 플레이어만 직접 위치를 변경 가능
        if (photonView.IsMine)
        {
            // 원점에서 반경 5유닛 내부의 랜덤한 위치 지정
            Vector3 randomSpawnPos = Random.insideUnitSphere * 5f;
            // 랜덤 위치의 y값을 0으로 변경
            randomSpawnPos.y = 0f;

            // 지정된 랜덤 위치로 이동
            transform.position = randomSpawnPos;
        }

        // 컴포넌트들을 리셋하기 위해 게임 오브젝트를 잠시 껐다가 다시 켜기
        // 컴포넌트들의 OnDisable(), OnEnable() 메서드가 실행됨
        gameObject.SetActive(false);
        gameObject.SetActive(true);
    }
}