using Photon.Pun;
using UnityEngine;

// Coin 스크립트 또한 Destroy() 메서드를 PhotonNetwork.Destroy() 메서드로 대체한 것 외에는 변경 사항이 없습니다.
// 19.6절 ‘네트워크 게임 매니저’에서 살펴볼 변경된 GameManager 스크립트는 호스트에서 점수를 갱신하면 
// 자동으로 모든 클라이언트에 갱신된 점수가 적용되도록 작성되었기 때문입니다. 따라서 RPC를사용하지 않았습니다.

// 게임 점수를 증가시키는 아이템
public class Coin : MonoBehaviourPun, IItem
{
    public int score = 200; // 증가할 점수

    public void Use(GameObject target)
    {
        // 게임 매니저로 접근해 점수 추가
        GameManager.instance.AddScore(score);
        // 모든 클라이언트에서의 자신을 파괴
        PhotonNetwork.Destroy(gameObject);
    }
}