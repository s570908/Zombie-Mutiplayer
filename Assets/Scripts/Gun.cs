using System.Collections;
using Photon.Pun;
using UnityEngine;

/*

• 기존 기능 : 사격 실행，사격 이펙트 재생, 재장전 실행，탄알 관리
• 변경된 기능 : 실제 사격 처리 부분을 호스트에서만 실행, 상태 동기화

새로운 Gun 스크립트에는 다음과 같은 주요 변경 사항이 적용되었습니다.
    • MonoBehaviourPun 사용
    • IPunObservable 인터페이스 상속, OnPhotonSerializeView() 메서드 구현 • 새로운 RPC 메서드 AddAmmo () 추가
    • Shot ()의 사격 처리 부분을 새로운 RPC 메서드 ShotProcessOnServer ()로 옮김 
    • ShotEffect ()를 새로운 RPC 메서드 ShotEffectPocessOnClients ()로 감쌈

IPunObservable 인터페이스와 OnPhotonSerializeView() 메서드
======================================================
Photon View 컴포넌트를 사용해 동기화를 구현할모든 컴포넌트(스크립트)는 IPunObservable 인터페이스를 상속하고 OnPhotonSerializeView () 메서드를 
구현해야 합니다. OnPhotonSeria lizeView () 메서드는 Photon View 컴포넌트를 사용해 로컬과 리모트 사이에서 어떤 값을 어떻게 주고받을지 결정합니다.

IPunObservable 인터페이스를 상속한 컴포넌트는 Photon View 컴포넌트의 Observed Components에 등록되어 로컬과 리모트에서 동기화될 수 있습니다.
Gun 스크립트에 추가된 OnPhotonSerializeView() 메서드를 살펴봅시다. 해당 메서드는Photon View 컴포넌트에 의해 자동으로 실행됩니다.
OnPhotonSerializeView() 메서드는 ammoRemain，magAmmo, state 값을 로컬에서 리모트 방향으로 동기화합니다. 
남은 전체 탄알，탄창의 탄알，총의 상태가 클라이언트 사이에서 동기화됩니다.

OnPhotonSerializeView () 메서드의 입력으로 들어오는 stream 은 현재 클라이언트에서 다른 클라이언트로 보낼 값을 쓰거나，
다른 클라이언트가 보내온 값을 읽을 때 사용할 스트림 형의 데이터 컨테이너입니다.

stream.IsWriting은 현재 스트림이 쓰기 모드인지 반환합니다. 현재 게임 오브젝트가 로컬 게임 오브젝트면 쓰기 모드가 되어 true, 
리모트 게임 요브젝트면 읽기 모드가 되어 false가 반환됩니다. 즉，클라이언트 A와 B가 있다고 가정했을 때 A에서 플레이어 캐릭터 a가 들고 있는 
총의 Gun 스크립트에서는 stream.IsWriting이 true입니다. 따라서 SendNext () 메서드로 스트림에 값을 삽입하여 네트워크를 통해 전송합니다.
클라이언트 B에서의 플레이어 캐릭터 a가 들고 있는 총의 Gun 스크립트에서는 stream.IsWriting이 false입니다. 
따라서 스트림으로 들어온 값을 ReceiveNext () 메서드로 가져옵니다. 이렇게 클라이언트 B의 a의 총은 클라이언트 A의 a의 총이 가지고 있는 값으로 동기화 됩니다.


*/

// 총을 구현한다
public class Gun : MonoBehaviourPun, IPunObservable
{
    // Photon View 컴포넌트를 사용해 동기화를 구현한 모든 컴포넌트(스크립트)는 
    //  1. IPunObservable 인터패이스를 상속하고,
    //      -. IPunObservable 인터페이스를 상속한 컴포넌트는 Photon View 컴포넌트의 Observed Components
    //          에 등록되어 로컬과 리모트에서 동기화될 수 있습니다.
    //  2. OnPhotonSerializeView() 메서드를 구현하여야 한다.  

    // 총의 상태를 표현하는데 사용할 타입을 선언한다
    public enum State
    {
        Ready, // 발사 준비됨
        Empty, // 탄창이 빔
        Reloading // 재장전 중
    }

    public State state { get; private set; } // 현재 총의 상태

    public Transform fireTransform; // 총알이 발사될 위치

    public ParticleSystem muzzleFlashEffect; // 총구 화염 효과
    public ParticleSystem shellEjectEffect; // 탄피 배출 효과

    private LineRenderer bulletLineRenderer; // 총알 궤적을 그리기 위한 렌더러

    private AudioSource gunAudioPlayer; // 총 소리 재생기

    public GunData gunData; // 총의 현재 데이터

    private float fireDistance = 50f; // 사정거리

    public int ammoRemain = 100; // 남은 전체 탄약
    public int magAmmo; // 현재 탄창에 남아있는 탄약

    private float lastFireTime; // 총을 마지막으로 발사한 시점

    // Photon View 컴포넌트에 의해 주기적으로 자동 실행되는, 동기화 메서드
    // ammoRemmain, magRemain, state값을 로컬에서 리모트로 동기화합니다. 
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // 현재 게임오브젝트가 로컬 오브젝트라면 IsWriting은 true가 된다.  
        // 리모트 오브젝트라면 IsWriting은 false가 된다.         
        // 로컬 오브젝트라면 쓰기 부분이 실행됨
        if (stream.IsWriting)
        {
            // 남은 탄약수를 네트워크를 통해 보내기
            stream.SendNext(ammoRemain);
            // 탄창의 탄약수를 네트워크를 통해 보내기. "탄창"을 영어로 번역하면 "magazine"입니다.
            stream.SendNext(magAmmo);
            // 현재 총의 상태를 네트워크를 통해 보내기
            stream.SendNext(state);
        }
        else
        {
            // 리모트 오브젝트라면 읽기 부분이 실행됨
            // 남은 탄약수를 네트워크를 통해 받기
            ammoRemain = (int)stream.ReceiveNext();
            // 탄창의 탄약수를 네트워크를 통해 받기
            magAmmo = (int)stream.ReceiveNext();
            // 현재 총의 상태를 네트워크를 통해 받기
            state = (State)stream.ReceiveNext();
        }
    }

    // 클라이언트 B가 자신의 로컬 플레이어 캐릭터 b를 움직여 탄알 아이템을 먹었을 때 위치가 동기화되므로
    // 클라이언트 A의 리모트 플레이어 캐릭터 b도 탄알 아이템을 먹게 된다.
    // PlayerHealth 스크립트에서 확인해 보았듯이, 클라이언트 B의 플레이어 캐릭터 b는 아이템을 사용할 수 없습니다.
    // 실제 탄알 아이템의 사용은 호스트 클라이언트인 A의 리모트 플레이어 캐릭터 b에서 실행됩니다. 
    // 아이템의 사용은 호스트 클라이언트 A에서만 실행됩니다: PlayerHealth.cs onTriggerEnter(), p.853 참조
    // 따라서 호스트 클라이언트 A는 모든 클라이언트 A와 B에서 b의 탄알이 증가하도록 RPC를 통해서 AddAmmo()를 실행할 겁니다.

    // 남은 탄약을 추가하는 메서드
    [PunRPC]
    public void AddAmmo(int ammo)
    {
        ammoRemain += ammo;
    }

    private void Awake()
    {
        // 사용할 컴포넌트들의 참조를 가져오기
        gunAudioPlayer = GetComponent<AudioSource>();
        bulletLineRenderer = GetComponent<LineRenderer>();

        // 사용할 점을 두개로 변경
        bulletLineRenderer.positionCount = 2;
        // 라인 렌더러를 비활성화
        bulletLineRenderer.enabled = false;
    }


    private void OnEnable()
    {
        // 전체 예비 탄약 양을 초기화
        ammoRemain = gunData.startAmmoRemain;
        // 현재 탄창을 가득채우기
        magAmmo = gunData.magCapacity;

        // 총의 현재 상태를 총을 쏠 준비가 된 상태로 변경
        state = State.Ready;
        // 마지막으로 총을 쏜 시점을 초기화
        lastFireTime = 0;
    }


    // 발사 시도
    public void Fire()
    {
        // 현재 상태가 발사 가능한 상태
        // && 마지막 총 발사 시점에서 timeBetFire 이상의 시간이 지남
        if (state == State.Ready
            && Time.time >= lastFireTime + gunData.timeBetFire)
        {
            // 마지막 총 발사 시점을 갱신
            lastFireTime = Time.time;
            // 실제 발사 처리 실행
            Shot();
        }
    }


    // Shot()과 ShotEffect()에서 실제 발사 처리는 호스트에 맡깁니다. 
    // 클라이언트 A, B, C가 존재하며, A가 호스트라고 가정하자. 
    // 1. 클라이언트 B의 로컬 플레이어 b의 총에서 Shot() 메서드를 실행
    // 2. Shot()에서 photonView.RPC("ShotProcessOnServer", Rpc.MasterClient); 실행
    // 3. 실제 사격 처리를 하는 ShotProcessOnServer()는 호스트 클라이언트 A에서만 실행
    // 4. 발사 이펙트 재생, 이펙트 재생은 모든 클라이언트들에서 실행
    private void Shot()
    {
        // 실제 발사 처리는 호스트에게 대리
        photonView.RPC("ShotProcessOnServer", RpcTarget.MasterClient);

        // 남은 탄환의 수를 -1
        magAmmo--;
        if (magAmmo <= 0)
        {
            // 탄창에 남은 탄약이 없다면, 총의 현재 상태를 Empty으로 갱신
            state = State.Empty;
        }
    }

    // 호스트에서 실행되는, 실제 발사 처리
    [PunRPC]
    private void ShotProcessOnServer()
    {
        // 레이캐스트에 의한 충돌 정보를 저장하는 컨테이너
        RaycastHit hit;
        // 총알이 맞은 곳을 저장할 변수
        Vector3 hitPosition = Vector3.zero;

        // 레이캐스트(시작지점, 방향, 충돌 정보 컨테이너, 사정거리)
        if (Physics.Raycast(fireTransform.position,
            fireTransform.forward, out hit, fireDistance))
        {
            // 레이가 어떤 물체와 충돌한 경우

            // 충돌한 상대방으로부터 IDamageable 오브젝트를 가져오기 시도
            IDamageable target =
                hit.collider.GetComponent<IDamageable>();

            // 상대방으로 부터 IDamageable 오브젝트를 가져오는데 성공했다면
            if (target != null)
            {
                // 상대방의 OnDamage 함수를 실행시켜서 상대방에게 데미지 주기
                target.OnDamage(gunData.damage, hit.point, hit.normal);
            }

            // 레이가 충돌한 위치 저장
            hitPosition = hit.point;
        }
        else
        {
            // 레이가 다른 물체와 충돌하지 않았다면
            // 총알이 최대 사정거리까지 날아갔을때의 위치를 충돌 위치로 사용
            hitPosition = fireTransform.position +
                          fireTransform.forward * fireDistance;
        }

        // 발사 이펙트 재생, 이펙트 재생은 모든 클라이언트들에서 실행
        photonView.RPC("ShotEffectProcessOnClients", RpcTarget.All, hitPosition);
    }

    // 이펙트 재생 코루틴을 랩핑하는 메서드
    [PunRPC]
    private void ShotEffectProcessOnClients(Vector3 hitPosition)
    {
        StartCoroutine(ShotEffect(hitPosition));
    }

    // 발사 이펙트와 소리를 재생하고 총알 궤적을 그린다
    private IEnumerator ShotEffect(Vector3 hitPosition)
    {
        // 총구 화염 효과 재생
        muzzleFlashEffect.Play();
        // 탄피 배출 효과 재생
        shellEjectEffect.Play();

        // 총격 소리 재생
        gunAudioPlayer.PlayOneShot(gunData.shotClip);

        // 선의 시작점은 총구의 위치
        bulletLineRenderer.SetPosition(0, fireTransform.position);
        // 선의 끝점은 입력으로 들어온 충돌 위치
        bulletLineRenderer.SetPosition(1, hitPosition);
        // 라인 렌더러를 활성화하여 총알 궤적을 그린다
        bulletLineRenderer.enabled = true;

        // 0.03초 동안 잠시 처리를 대기
        yield return new WaitForSeconds(0.03f);

        // 라인 렌더러를 비활성화하여 총알 궤적을 지운다
        bulletLineRenderer.enabled = false;
    }

    // 재장전 시도
    public bool Reload()
    {
        if (state == State.Reloading ||
            ammoRemain <= 0 || magAmmo >= gunData.magCapacity)
        {
            // 이미 재장전 중이거나, 남은 총알이 없거나
            // 탄창에 총알이 이미 가득한 경우 재장전 할수 없다
            return false;
        }

        // 재장전 처리 실행
        StartCoroutine(ReloadRoutine());
        return true;
    }

    // 실제 재장전 처리를 진행
    private IEnumerator ReloadRoutine()
    {
        // 현재 상태를 재장전 중 상태로 전환
        state = State.Reloading;
        // 재장전 소리 재생
        gunAudioPlayer.PlayOneShot(gunData.reloadClip);

        // 재장전 소요 시간 만큼 처리를 쉬기
        yield return new WaitForSeconds(gunData.reloadTime);

        // 탄창에 채울 탄약을 계산한다
        int ammoToFill = gunData.magCapacity - magAmmo;

        // 탄창에 채워야할 탄약이 남은 탄약보다 많다면,
        // 채워야할 탄약 수를 남은 탄약 수에 맞춰 줄인다
        if (ammoRemain < ammoToFill)
        {
            ammoToFill = ammoRemain;
        }

        // 탄창을 채운다
        magAmmo += ammoToFill;
        // 남은 탄약에서, 탄창에 채운만큼 탄약을 뺸다
        ammoRemain -= ammoToFill;

        // 총의 현재 상태를 발사 준비된 상태로 변경
        state = State.Ready;
    }
}