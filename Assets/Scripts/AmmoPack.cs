using Photon.Pun;
using UnityEngine;

// 기존 AmmoPack 스크립트는 플레이어 캐릭터가 가지고 있는 Gun 게임 오브젝트의 Gun 스크립트로 접근하여 
// 남은 탄알 수를 증가시켰습니다. 이때 변경 전의 AmmoPack 스크립트를 사용할 경우 호스트에서만 총의 남은 탄알이 증가합니다.
// 19.2.5절 ‘PlayerHealth 스크립트’에서 아이템들의 Use() 메서드를 호스트에서만 실행하도록 변경했기 때문입니다.
// 또한 Gun 스크립트는 로컬에서 리모트 방향으로 남은 탄알을 항상 동기화하지만，호스트에서 다른 클라이언트 방향으로는 
// 자동 동기화를 하지 않습니다. 즉，호스트 A에서의 플레이어 B의 탄알이 증가할 경우 다른 클라이언트로 동기화가 이루어지지 않습니다.
// 따라서 모든 클라이언트에서 원격으로 AddAmmo() 메서드가 실행되도록 코드를 변경했습니다. 
// 즉，변경된 코드는 아이템 사용 자체는 호스트에서만 이루어지지만，아이템을 사용하여 탄알이
// 증가하는 효과는 모든 클라이언트에서 동일하게 적용되게 한 겁니다.

// 어떤 클라이언트에서 Destroy() 메서드를 실행하여 네트워크상의 게임 오브젝트 a를 파괴했 다고 가정합시다. 
// 게임 오브젝트 a는 해당 클라이언트에서는 존재하지 않지만，다른 클라이언 트에서는 멀쩡히 존재합니다.
// 따라서 네트워크상의 모든 클라이언트에서 동일하게 파괴되어야 하는 게임 오브젝트는 Destroy() 메서드 대신 
// PhotonNetwork.Destroy() 메서드를 사용합니다.
// PhotonNetwork.Destroy()는 Photon View 컴포넌트를 가지고 있는 게임 오브젝트를 입력받습니다. 
// 입력된 게임 오브젝트는 모든 클라이언트에서 동시에 파괴됩니다. 
// 즉，사용된 탄알 아이템이 호스트에서 PhotonNetwork.Destroy(gameObject);를 실행하면 호스트를 포함한 
// 모든 클라이언트에서 탄알 아이템 게임 오브젝트가 파괴됩니다.

// 총알을 충전하는 아이템
public class AmmoPack : MonoBehaviourPun, IItem {
    public int ammo = 30; // 충전할 총알 수

    public void Use(GameObject target) {
        // 전달 받은 게임 오브젝트로부터 PlayerShooter 컴포넌트를 가져오기 시도
        PlayerShooter playerShooter = target.GetComponent<PlayerShooter>();

        // PlayerShooter 컴포넌트가 있으며, 총 오브젝트가 존재하면
        if (playerShooter != null && playerShooter.gun != null)
        {
            // 총의 남은 탄환 수를 ammo 만큼 더하기, 모든 클라이언트에서 실행
            playerShooter.gun.photonView.RPC("AddAmmo", RpcTarget.All, ammo);
        }

        // 모든 클라이언트에서의 자신을 파괴
        PhotonNetwork.Destroy(gameObject);
    }
}