using Photon.Pun;
using UnityEngine;

// HealthPack은 AmmoPack과 달리 RPC를 사용하지 않습니다. 19.2.4절 ‘LivingEntity 스크립트’ 에서 확인한 
// RestoreHealth() 메서드는 호스트에서 실행하면 자동으로 다른 클라이언트에서도 원격 실행되기 때문입니다.
// HealthPack 스크립트의 Use() 메서드 마지막에는 네트워크상의 모든 클라이언트에서 체력 아이템을 파괴하도록 
// PhotonNetwork.Destroy(gameObject);를 실행합니다.

// 체력을 회복하는 아이템
public class HealthPack : MonoBehaviourPun, IItem
{
    public float health = 50; // 체력을 회복할 수치

    public void Use(GameObject target)
    {
        // 전달받은 게임 오브젝트로부터 LivingEntity 컴포넌트 가져오기 시도
        LivingEntity life = target.GetComponent<LivingEntity>();

        // LivingEntity컴포넌트가 있다면
        if (life != null)
        {
            // 체력 회복 실행
            life.RestoreHealth(health);
        }

        // 모든 클라이언트에서의 자신을 파괴
        PhotonNetwork.Destroy(gameObject);
    }
}