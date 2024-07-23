using System;
using Photon.Pun;
using UnityEngine;

/******

기존의 LivingEntity 스크립트는 체력과 사망 상태를 관리했습니다. 네트워크에서 동작하는 LivingEntity도 
체력과 사망 상태를 관리하되，그 값들을 클라이언트 사이에서 동기화해야 합니다. 그런데 체력과 사망 상태는 
게임 승패에 매우 민감한 처리입니다. 

따라서 LivingEntity의 체력과 사망 상태에 관련된 처리가 무조건 호스트에서 실행되고, 
그 결과를 클라이언트가 받아들이 도록 구현해야 합니다(18.3.1 절 ‘호스트에 위임’참조).

새로운 LivingEntity 스크립트의 주요 변경 사항은 다음과 같습니다.
    • MonoBehaviourPun 사용
    • 체력, 사망 상태 동기화를 위한 ApplyUpdatedHealth() 메서드 추가 
    • OnDamage(), RestoreHealth()에 [PunRPC] 선언
    • OnDamage()에서 대미지 처리는 호스트에서만 실행
    • RestoreHealth()에서 체력 추가 처리는 호스트에서만 실행

[PunRPC]는 18.3.2절 ‘RPC’에서 설명한 RPC를 구현하는 속성입니다. [PunRPC]로 선언된 메서드는 
다른 클라이언트에서 원격 실행할 수 있습니다. DoSomething()이라는 메서드가 [PunRPC]로 선언되었다고 가정해봅시다.
클라이언트 A는 자신의 월드에서는 DoSomething()을 실행하지 않고，클라이언트 B에 있는 DoSomething()을 
원격 실행할 수 있습니다. A 자신을 포함한 모든 클라이 언트에서 DoSomething() 메서드를 실행시키는 것도 가능합니다.

RPC를 통해 어떤 메서드를 다른 클라이언트에서 원격 실행할 때는 Photon View 컴포넌트의 RPC() 메서드를 사용합니다.
RPC () 메서드는 입력으로 다음 값을 받습니다.
    • 원격 실행할 메서드 이름(string 타입)
    • 원격 실행할 대상 클라이언트(RpcTai-get 타입) 
    • 원격 실행할 메서드에 전달할 값(필요한 경우)

예를 들어 아래 코드는 자신의 Photon View 컴포넌트를 사용해 DoSomething() 메서드를 모든 클라이언트에서 
원격 실행합니다.

    photonView.RPC("DoSomething", RpcTarget.AU);

속성(Attribute)은 어떤 처리를 직접 실행하진 않지만 컴파일러에 해당 메서드나 변수에 대한 메타 정보를 알려주는 
키워드입니다. 프로퍼티(Property) 또한 속성으로 번역되지만 Attribute와 Property는 다른 개념입니다.

ApplyUpdateHealth()
====================

새로 추가된 ApplyUpdateHealth() 메서드는 [PunRPC] 속성으로 선언되었습니다.

[PunRPC]
public void ApplyUpdatedHealth(float newHealth, bool newDead) {
    health = newHealth;
    dead = newDead; 
}

ApplyUpdateHealth()는 새로운 체력값과 새로운 사망 상탯값을 받아 기존 변숫값을 갱신하는 단순한 메서드입니다. 
겉보기에는 ApplyUpdatedHealth() 메서드를 사용할 필요 없이 health와 dead 값을 직접 변경하는 게 나아보입니다.
ApplyllpdatedHealth()는 호스트 측 LivingEntity의 체력，사망 상탯값을 다른 클라이언트의 LivingEntity에 
전달하기 위해 사용됩니다.

게임 속에 LivingEntity 오브젝트 a가 있다고 가정해봅시다. 호스트 클라이언트는 a의 체력과 사망 상태를 변경합니다. 
동시에 같은 코드로 ApplyUpdateHealth() 메서드에 변경된 체력과 사망 상태를 입력하고，다른 모든 클라이언트에서 
원격 실행합니다.

photonView.RPCC'ApplyllpdatedHealth", RpcTarget.Others, health, dead);

그러면 호스트에서 a 의 체력과 사망상태가 다른 모든 클라이언트의 a 에 적용됩니다.

최종 완성된 빌드에서는 보안상의 이유로 OnDamage() 메서드의 최초 실행이 호스트에서만 이루어질 겁니다. 
다른 클라이언트에서는 OnDamage() 메서드를 RPC를 통해 간접 실행할 겁니다. 즉，위 OnDamage()는 
더블탭처럼 동작합니다.

먼저 호스트에서만 OnDamage()가 실행되고, 대미지 처리가 이루어집니다. 그리고 호스트의 OnDamage() 메서드가 
끝날 때 다른 모든 클라이언트의 OnDamage()를 원격 실행합니다. 따라서 OnDamage() 메서드의 실행이 
호스트에서 모든 클라이언트로 전파됩니다.
흐름을 표현하면 다음과 같습니다.
    1. 호스트에서의 LivingEntity가 공격을 맞아 OnDamage()가 실행됨
    2. 호스트에서 체력을 변경하고 클라이언트에 변경된 체력을 동기화
    3. 호스트가 다른 모든 클라이언트의 LivingEntity의 OnDamage()를 원격 실행

호스트가 아닌 클라이언트는 OnDamage()를 실행했을 때 if (PhotonNetwork.IsMasterClient) 로 묶인 
체력 처리 부분은 실행되지 않고，나머지 부가적인 처리만 실행된다는 사실에 주목합니다.

RestoreHealth() 메서드에서 체력이 실제로 변경되는 기존 부분은 OnDamage() 메서드와 마찬가지로 
if (PhotonNetwork.IsMasterClient)로 감쌌습니다. RestoreHealth() 메서드도 OnDamage() 메서드와 
같은 원리로 동작합니다. 호스트에서 RestoreHealth()를 가장 먼저 실행하고 체력을 변경합니다. 
그리고 ApplyUpdatedHealth() 메서드를 원격 실행하여 변경된 체력을 다른 클라이언트에 적용합니다.
또한 호스트의 RestoreHealth()가 종료되기 전 다른 모든 클라이언트에서 RestoreHealth() 를 원격 실행합니다. 
단，다른 모든 클라이언트에서 실행하는 RestoreHealth ()에서는 체력을 변경하는 코드는 실행되지 않고，
부가적인 처리만 실행됩니다.


 
********/

// 생명체로서 동작할 게임 오브젝트들을 위한 뼈대를 제공
// 체력, 데미지 받아들이기, 사망 기능, 사망 이벤트를 제공
public class LivingEntity : MonoBehaviourPun, IDamageable
{
    public float startingHealth = 100f; // 시작 체력
    public float health { get; protected set; } // 현재 체력
    public bool dead { get; protected set; } // 사망 상태
    public event Action onDeath; // 사망시 발동할 이벤트


    // 호스트->모든 클라이언트 방향으로 체력과 사망 상태를 동기화 하는 메서드
    [PunRPC]
    public void ApplyUpdatedHealth(float newHealth, bool newDead)
    {
        health = newHealth;
        dead = newDead;
    }

    // 생명체가 활성화될때 상태를 리셋
    protected virtual void OnEnable()
    {
        // 사망하지 않은 상태로 시작
        dead = false;
        // 체력을 시작 체력으로 초기화
        health = startingHealth;
    }

    // 데미지 처리
    // 호스트에서 먼저 단독 실행되고, 호스트를 통해 다른 클라이언트들에서 일괄 실행됨
    [PunRPC]
    public virtual void OnDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // 데미지만큼 체력 감소
            health -= damage;

            // 호스트에서 클라이언트로 동기화
            photonView.RPC("ApplyUpdatedHealth", RpcTarget.Others, health, dead);

            // 다른 클라이언트들도 OnDamage를 실행하도록 함
            photonView.RPC("OnDamage", RpcTarget.Others, damage, hitPoint, hitNormal);
        }

        // 체력이 0 이하 && 아직 죽지 않았다면 사망 처리 실행
        if (health <= 0 && !dead)
        {
            Die();
        }
    }


    // 체력을 회복하는 기능
    [PunRPC]
    public virtual void RestoreHealth(float newHealth)
    {
        if (dead)
        {
            // 이미 사망한 경우 체력을 회복할 수 없음
            return;
        }

        // 호스트만 체력을 직접 갱신 가능
        if (PhotonNetwork.IsMasterClient)
        {
            // 체력 추가
            health += newHealth;
            // 서버에서 클라이언트로 동기화
            photonView.RPC("ApplyUpdatedHealth", RpcTarget.Others, health, dead);

            // 다른 클라이언트들도 RestoreHealth를 실행하도록 함
            photonView.RPC("RestoreHealth", RpcTarget.Others, newHealth);
        }
    }

    public virtual void Die()
    {
        // onDeath 이벤트에 등록된 메서드가 있다면 실행
        if (onDeath != null)
        {
            onDeath();
        }

        // 사망 상태를 참으로 변경
        dead = true;
    }
}