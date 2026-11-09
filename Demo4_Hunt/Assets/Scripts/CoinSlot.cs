using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Scavenger.Vending;

/// <summary>
/// Manages the coin slot socket for vending machine.
/// Coins snap to this socket and are deposited when released.
/// </summary>
public class CoinSlot : MonoBehaviour
{
    [SerializeField] private Transform snapPosition;
    [SerializeField] private AudioSource depositSound;
    
    private XRSocketInteractor _socketInteractor;
    private Coin _currentCoin;

    public VendingMachine vendingMachine;
    private void Awake()
    {
        _socketInteractor = GetComponent<XRSocketInteractor>();
        
        if (_socketInteractor == null)
        {
            Debug.LogError("CoinSlot: No XRSocketInteractor found on this GameObject!");
        }

        if (depositSound == null)
        {
            depositSound = GetComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        if (_socketInteractor != null)
        {
            // Subscribe to socket events
            _socketInteractor.selectEntered.AddListener(OnCoinSnapped);
            _socketInteractor.selectExited.AddListener(OnCoinReleased);
        }
    }

    private void OnDisable()
    {
        if (_socketInteractor != null)
        {
            _socketInteractor.selectEntered.RemoveListener(OnCoinSnapped);
            _socketInteractor.selectExited.RemoveListener(OnCoinReleased);
        }
    }

    /// <summary>
    /// Called when a coin snaps into the socket
    /// </summary>
    private void OnCoinSnapped(SelectEnterEventArgs args)
    {
        _currentCoin = args.interactableObject.transform.GetComponent<Coin>();
        if (_currentCoin != null)
        {
            Debug.Log($"Coin deposited: denomination {_currentCoin.denomination}");
            // Play deposit sound
            if (depositSound != null)
            {
                depositSound.Play();
            }
            // Trigger deposit animation immediately
            _currentCoin.OnDeposited();
            //Call AddCredit() on the vending machine with the coin's denomination
            if (vendingMachine != null)
            {
                vendingMachine.AddCredit(_currentCoin.denomination);    
            }
        }
    }

    /// <summary>
    /// Called when a coin is released from the socket
    /// </summary>
    private void OnCoinReleased(SelectExitEventArgs args)
    {
        Coin coin = args.interactableObject.transform.GetComponent<Coin>();
        if (coin != null && coin != _currentCoin)
        {
            Debug.Log($"Coin removed from slot");
            _currentCoin = null;
        }
    }

    public Coin GetCurrentCoin() => _currentCoin;
}
