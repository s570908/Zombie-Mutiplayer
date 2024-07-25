using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

/*************************************************

웨이브 정보 동기화
===============

ZombieSpawner 스크립트에 의한 좀비 생성은 호스트의 로컬에서만 실행됩니다. 다른 클라이언트는 호스트가 생성한 
좀비 게임 오브젝트의 복제본을 네트워크를 통해 건네받습니다.

ZombieSpawner 스크립트는 남은 좀비와 현재 웨이브 수를 UI로 표시합니다. 
ZombieSpawner 스크립트는 생성한 좀비를 zombies 리스트에 추가하므로 남은 좀비 수는 zombies 리스트에 등록된 
요브젝트 수로 알 수 있습니다.

그런데 zombies 리스트에 생성한 좀비를 등록하는 절차는 호스트의 ZombieSpawner에서만 실행되고 다른 클라이언트에서는 
실행되지 않습니다. 

따라서 추가 변수 zombieCount를 선언했습니다. 그리고 호스트의 zombies.Count 값을 리모트의 zombieCount로 
전달하는 방식으로 남은 좀비 수를 다른 클라이언트에서 알 수 있게 합니다.
결론적으로 남은 좀비 수 zombieCount와 현재 웨이브 wave 값은 OnPhotonSerializeView() 메서드를 구현하여 
다음과 같이 동기화합니다: OnPhotonSerializeView() 참조

Zombie Spawenr 게임 오브젝트를 로컬로 가지고 있는 호스트에서는 zombies.Count를 남은 좀비 수를 파악하는 데 사용 가능하지만, 
다른 클라이언트에서는 zombieCount를 대신 사용해야 합니다.
따라서 현재 웨이브와 님은 좀비 수를 표시하던 UpdateUI() 메서드도 다음과 같이 변경했습니다: UpdateUI() 참조

Setup() 원격 실행
===============

좀비 서바이버 멀티플레이어에서는 네트워크상의 모든 클라이언트에서 같은 좀비를 생성해야 합니다.
따라서 CreateZombie() 메서드에서 Instantiate()를 사용하여 zombiePrefab의 복제본을 생성하던 부분을 
PhotonNetwork.Instantiate()를 사용하도록 변경했습니다.

그다음 생성한 좀비 zombie에서 Setup() 메서드를 실행하여 좀비의 능력치를 설정합니다. 단，현재 호스트 로컬에서만 
Setup()을 실행한 경우 다른 클라이언트에는 변경된 좀비의 능력치가 적용되지 않습니다.
따라서 호스트뿐만 아니라 모든 클라이언트에서 동시에 생성한 좀비의 Setup() 메서드를 원격 실행합니다:

zombie.photonView.RPC("Setup", RpcTarget.All, zombieData.health, zombieData.damage, 
    zombieData.speed, zombieData.skinColor);

그러면 해당 좀비의 능력치와 피부색이 모든 클라이언트에서 같아질 겁니다.
단, 이전에는 zombieData를 직접 Zombie의 Setup() 메서드에 전달하였지만 ZimbieData 에셋을 다른 클라이언트에 
그대로 네트워크를 통해 전송하기에는 필요한 바이트 용량이 큽니다. 따라서 수정된 Setup() 메서드에서는 ZombieData
대신 ZombieData의 각 필드(health, damage, speed, skinColor)를 전달했다는 점에 주의합니다.

좀비 사망 이벤트
============
CreateZombie() 메서드 마지막 부분에는 Zombie의 onDeath 이벤트에 생성한 좀비가 사망할 경우 실행될 
메서드를 등록했습니다.

CreateZombie() 메서드가 호스트에서만 실행되므로 onDeath 이벤트에 이벤트 리스너를 등록하는 위 코드는 
호스트에서만 실행됩니다. 따라서 좀비 사망 시 사망한 좀비를 리스트에서 제거하고，사망한 좀비를 10초 뒤에 파괴하며，
게임 매니저에 100점을 추가하는 처리는 호스트에서만 실행됩니다.

여기서 좀비 리스트에서 사망한 좀비를 제거하고 게임 매니저에 점수를 추가하는 처리는 호스트에서만 실행해도 됩니다. 
남은 좀비 수와 현재 게임 점수는 호스트에서 변경되었을 때 자동으로 다른 클라이언트에도 반영되도록 구현되었기 때문입니다.

하지만 좀비가 파괴되는 처리는 다른 클라이언트에 자동 반영되지 않습니다. 따라서 Destroy() 메서드를 
PhotonNetwork.Destroy() 메서드로 대체하여 호스트에서 좀비가 파괴될 때 다른 모든 클라이언트에서도 좀비가 파괴되게 
해야 합니다. 하지만 PhotonNetwork.Destroy() 메서드는 지연시간을 받지 않습니다. 
따라서 PhotonNetwork.Destroy() 메서드를 지연하여 실행하는 코루틴 메서드를 만들어 
기존 Destroy() 메서드를 대체했습니다.

IEnumerator DestroyAfter(GameObject target, float delay)를 사용하여 
기존 zombie.onDeath +=()=> Destroy(zombie.gameObject, 10f);를 다음 코드로 대체했습니다.
zombie.onDeath += () => StartCoroutineCDestroyAfter(zombie.gameObject, 10f));

직렬화와 역직렬화
==============
PUN은 RPC로 원격 실행할 메서드에 함께 첨부할 수 있는 입력 타입에 제약이 있습니다. RPC를 통해 다른 클라이언트로 
전송 가능한 대표적인 타입으로는 byte, bool, int, float, string, Vector3, Quaternion이 있습니다. 
이들은 직렬화/역직렬화가 PUN에 의해 자동으로 이루어집니다.

본래 Color 타입의 값은 RPC 메서드의 입력으로 첨부할 수 없습니다. 따라서 CreateEnemy() 메서드에서 
Setup() 메서드의 RPC 실행에 skinColor를 입력으로 넘겨줄 수 없습니다. 단，기존에 RPC에서 지원하지 않던 타입을 
여러분이 직접 지원하도록 정의하는 것은 가능합니다. PhotonPeer.RegisterType() 메서드를 실행하고, 
원히는 타입을 명시하고, 어떻게 해당 타입을 직렬화(시리얼라이즈/Serialize)/역직렬화(디시리얼라이즈/Deserialize)할지 
명시하면 됩니다. PhotonPeer.RegisterType(타입, 번호, 직렬화 메서드, 역직렬화 메서드);

추상적인 오브젝트는 물리적인 통신 회선을 통해 ‘그냥’ 전송할 수 없습니다. 따라서 오브젝트를 물리적인 회선으로 전송하려면 
해당 오브젝트를 ‘날것/Raw’ 그대로의 타입인 바이트 데이터로 변경해야 합니다. 직렬화는 어떤 오브젝트를 
바이트 데이터로 변환하는 처리입니다. 역직렬화는 바이트 데이터를 다시 원본 오브젝트로 변환하는 처리입니다. 
송신 측은 오브젝트를 직렬화해 바이트 데이터로 변경하여 보내고，수신 측은 받은 바이트 데이터를 역직렬화해 원본 데이터로 복구합니다.

PhotonPeer.RegisterType() 메서드에서 원하는 타입에 대한 직렬화와 역직렬화 메서드를 등록하면 PUN이 
해당 메서드를 네트워크상에서 해당 타입을 주고받는 데 사용합니다. 따라서 ZombieSpawner의 Awake() 메서드에 
다음과 같은 처리를 추가했습니다.

void Awake() {
    PhotonPeer.RegisterType(typeof(Color), 128, Colorserialization.SerializeColor,
        Colorserialization.DeserializeColor);
}

위 코드는 Color 타입을 RPC로 전송 가능하게 PUN에 등록합니다. 사용한 숫자 128은 이미 등록된 다른 타입과 겹치지 
않도록 무작위로 선택한 숫자입니다. RPC 전송에 사용할 커스텀 타입은 255개까지 등록 가능하며，각각의 타입은 
고유 번호를 할당받아야 합니다. ColorSerialization.SerializeColor()와 
ColorSerialization.DeserializeColor()는 저자가 미리 만들어둔 컬러 직렬화와 역직렬화 메서드입니다. 
각각 컬러를 바이트 데이터로，바이트 데이터를 다시 컬러로 변환합니다.
해당 메서드들은 프로젝트의 Scripts 폴더에 있는 Colorserialization 스크립트에서 확인 가능합니다.

***************************************************/


// 좀비 게임 오브젝트를 주기적으로 생성
public class ZombieSpawner : MonoBehaviourPun, IPunObservable
{
    public Zombie zombiePrefab; // 생성할 좀비 원본 프리팹

    public ZombieData[] zombieDatas; // 사용할 좀비 셋업 데이터들
    public Transform[] spawnPoints; // 좀비 AI를 소환할 위치들

    private List<Zombie> zombies = new List<Zombie>(); // 생성된 좀비들을 담는 리스트

    private int zombieCount = 0; // 남은 좀비 수
    private int wave; // 현재 웨이브

    // 주기적으로 자동 실행되는, 동기화 메서드
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // 로컬 오브젝트라면 쓰기 부분이 실행됨
        if (stream.IsWriting)
        {
            // 남은 좀비 수를 네트워크를 통해 보내기
            stream.SendNext(zombies.Count);
            // 현재 웨이브를 네트워크를 통해 보내기
            stream.SendNext(wave);
        }
        else
        {
            // 리모트 오브젝트라면 읽기 부분이 실행됨
            // 남은 좀비 수를 네트워크를 통해 받기
            zombieCount = (int)stream.ReceiveNext();
            // 현재 웨이브를 네트워크를 통해 받기 
            wave = (int)stream.ReceiveNext();
        }
    }

    private void Awake()
    {
        PhotonPeer.RegisterType(typeof(Color), 128, ColorSerialization.SerializeColor,
            ColorSerialization.DeserializeColor);
    }

    private void Update()
    {
        // 호스트만 좀비를 직접 생성할 수 있음
        // 다른 클라이언트들은 호스트가 생성한 좀비를 동기화를 통해 받아옴
        if (PhotonNetwork.IsMasterClient)
        {
            // 게임 오버 상태일때는 생성하지 않음
            if (GameManager.instance != null && GameManager.instance.isGameover)
            {
                return;
            }

            // 좀비들을 모두 물리친 경우 다음 스폰 실행
            if (zombies.Count <= 0)
            {
                SpawnWave();
            }
        }

        // UI 갱신
        UpdateUI();
    }

    // 웨이브 정보를 UI로 표시
    private void UpdateUI()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // 호스트는 직접 갱신한 좀비 리스트를 통해 남은 좀비의 수를 표시함
            UIManager.instance.UpdateWaveText(wave, zombies.Count);
        }
        else
        {
            // 클라이언트는 좀비 리스트를 갱신할 수 없으므로, 호스트가 보내준 zombieCount를 통해 좀비의 수를 표시함
            UIManager.instance.UpdateWaveText(wave, zombieCount);
        }
    }

    // 현재 웨이브에 맞춰 좀비를 생성
    private void SpawnWave()
    {
        // 웨이브 1 증가
        wave++;

        // 현재 웨이브 * 1.5에 반올림 한 개수 만큼 좀비를 생성
        int spawnCount = Mathf.RoundToInt(wave * 1.5f);

        // spawnCount 만큼 좀비 생성
        for (int i = 0; i < spawnCount; i++)
        {
            // 좀비 생성 처리 실행
            CreateZombie();
        }
    }

    // 좀비 생성
    private void CreateZombie()
    {
        // 사용할 좀비 데이터 랜덤으로 결정
        ZombieData zombieData = zombieDatas[Random.Range(0, zombieDatas.Length)];

        // 생성할 위치를 랜덤으로 결정
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // 좀비 프리팹으로부터 좀비 생성, 네트워크 상의 모든 클라이언트들에게 생성됨
        GameObject createdZombie = PhotonNetwork.Instantiate(zombiePrefab.gameObject.name,
            spawnPoint.position,
            spawnPoint.rotation);

        // 생성한 좀비를 셋업하기 위해 Zombie 컴포넌트를 가져옴
        Zombie zombie = createdZombie.GetComponent<Zombie>();

        // 호스트뿐만 아니라 모든 클라이언트에서 동시에 생성한 좀비의 Setup () 메서드를 원격 실행합니다.
        // 그러면 해당 좀비의 능력치와 피부색이 모든 클라이언트에서 같아질 겁니다.

        // 생성한 좀비의 능력치 설정
        zombie.photonView.RPC("Setup", RpcTarget.All, zombieData.health, zombieData.damage, zombieData.speed, zombieData.skinColor);

        // 호스트 클라이언트에서만 수행된다. 다른 클라이언트에서는 수행되지 않는다. 
        // 호스트 클라이언트는 좀비의 총 숫자를 zombies.Count로 알 수 있다. 그러나 다른  클라이언트는 그 숫자를 알 수가 없다. 
        // 호스트 클라이언트는 다른 모든 클라이언트에게 zombies.Count를 송부하고 다른 모든 클라리언트는 이것을 zombiesCount 변수에 받아서 담는다.
        // Zombie Spawner 게임 오브젝트를 로컬로 가지고 있는 호스트에서는 zombies.Count를 남은 좀비 수를 파악하는 데 사용 가능하지만, 
        // 다른 클라이언트에서는 zombieCount를 대신 사용해야 합니다.
        // 참조: OnPhotonSerializeView()

        // 생성된 좀비를 리스트에 추가
        zombies.Add(zombie);

        // onDeath 이벤트에 이벤트 리스너를 등록하는 아래의 코드는 호스트에서만 실행됩니다. 
        // 따라서 좀비 사망 시 사망한 좀비를 리스트에서 제거하고，사망한 좀비를 10초 뒤에 파괴하며，게임 매니저에 100점 을 추가하는 처리는 
        // 호스트에서만 실행됩니다.

        // 여기서 좀비 리스트에서 사망한 좀비를 제거하고 게임 매니저에 점수를 추가하는 처리는 호스트에서만 실행해도 됩니다. 
        // 남은 좀비 수와 현재 게임 점수는 호스트에서 변경되었을 때 자동으로 다른 클라이언트에도 반영되도록 구현되었기 때문입니다.
        // 참조: OnPhotonSerializeView()

        // 좀비의 onDeath 이벤트에 익명 메서드 등록
        // 사망한 좀비를 리스트에서 제거
        zombie.onDeath += () => zombies.Remove(zombie);

        // 하지만 좀비가 파괴되는 처리는 다른 클라이언트에 자동 반영되지 않습니다. 따라서 Destroy () 메서드를 PhotonNetwork.Destroy () 메서드로 대체하여 
        // 호스트에서 좀비가 파괴될 때 다른 모든 클라이언트에서도 좀비가 파괴되게 해야 합니다.
        // 참조: DestroyAfter(zombie.gameObject, 10f)의 PhotonNetwork.Destroy(target)

        // 사망한 좀비를 10 초 뒤에 파괴
        zombie.onDeath += () => StartCoroutine(DestroyAfter(zombie.gameObject, 10f));
        // 좀비 사망시 점수 상승
        zombie.onDeath += () => GameManager.instance.AddScore(100);
    }

    // 포톤의 Network.Destroy()는 지연 파괴를 지원하지 않으므로 지연 파괴를 직접 구현함
    IEnumerator DestroyAfter(GameObject target, float delay)
    {
        // delay 만큼 쉬고
        yield return new WaitForSeconds(delay);

        // target이 아직 파괴되지 않았다면
        if (target != null)
        {
            // target을 모든 네트워크 상에서 파괴
            PhotonNetwork.Destroy(target);
        }
    }
}