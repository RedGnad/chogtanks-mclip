using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Collider2D))]
public class ExplosivePowerup : MonoBehaviourPun
{
    [Header("SFX")]
    [SerializeField] private AudioClip pickupSFX;
    [SerializeField] private float pickupVolume = 2.5f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // We only want the master client to handle the logic
        if (!PhotonNetwork.IsMasterClient) return;

        // Check if the object that entered the trigger has a PhotonView and belongs to a player
        TankShoot2D tank = other.GetComponentInParent<TankShoot2D>();
        if (tank != null && tank.photonView != null && tank.photonView.Owner != null)
        {
            // Use an RPC to grant the power-up to the specific client who owns the tank
            tank.photonView.RPC("RPC_ActivateExplosivePowerup", tank.photonView.Owner);

            photonView.RPC("RPC_PlayPickupFX", RpcTarget.All);
            StartCoroutine(DestroyNextFrame());
        }
    }

    [PunRPC]
    void RPC_PlayPickupFX()
    {
        if (pickupSFX != null && SFXManager.Instance != null)
        {
            SFXManager.Instance.audioSource.PlayOneShot(pickupSFX, pickupVolume);
        }
    }
    private System.Collections.IEnumerator DestroyNextFrame()
    {
        // Wait one frame so pickup RPC reaches everyone before destroy
        yield return null;
        if (gameObject != null)
        {
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
