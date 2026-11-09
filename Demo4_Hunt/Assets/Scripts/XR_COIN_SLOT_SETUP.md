# XR Coin Slot Setup Guide

## Overview
This system uses Unity's XR Interaction Toolkit to allow coins to be grabbed, snapped into a vending machine slot, and deposited.

## Setup Instructions

### 1. Coin GameObject Setup
1. Select your coin prefab/GameObject in the scene
2. Add a **Rigidbody** component:
   - Mass: 0.1 (light)
   - Constraints: Freeze Rotation (to prevent spinning)
   - Collision Detection: Continuous
3. Add an **XRGrabInteractable** component (from Unity XR Interaction Toolkit):
   - Enable "Throw on Release"
4. Ensure it has a **Collider** (Box or Capsule work fine)
5. Add the **Coin.cs** script component
   - Set the denomination (0-3)

### 2. Coin Slot GameObject Setup
1. Create an empty GameObject for the coin slot (place where coins should snap)
2. Add a **Rigidbody** component:
   - Body Type: Kinematic
   - Collision Detection: Continuous
3. Add an **XRSocketInteractor** component (from Unity XR Interaction Toolkit):
   - Set "Create Inter. Layer Mask" or assign appropriate layers
   - Configure socket settings (hover distance, etc.)
4. Add a **Collider** (Sphere works well) to define the socket acceptance area
5. Add the **CoinSlot.cs** script component

### 3. Layer Configuration
1. Create or use a layer for coins (e.g., "Coin")
2. Create or use a layer for coin slots (e.g., "CoinSlot")
3. In each XR Interactor, configure the **Interaction Layer Mask** to match appropriately

### 4. Collision Settings
In ProjectSettings > Physics:
- Ensure the Coin layer can collide with CoinSlot layer
- Other collisions can be disabled between coin and coin

## How It Works

1. **Grab**: Player grabs coin with hand - XR Grab Interactable handles this
2. **Snap**: When coin enters socket trigger area - Socket Interactor snaps it into place
3. **Release**: When player releases while over socket - Coin stays in socket
4. **Deposit**: Call `coin.OnDeposited()` to finalize the transaction:
   - Disables physics
   - Hides the coin
   - Triggers any deposit logic

## Optional: Trigger Automatic Deposit

If you want coins to deposit automatically after being in the slot for a time:

```csharp
// In CoinSlot.cs, add to OnCoinSnapped:
StartCoroutine(AutoDepositCoin(_currentCoin));

private IEnumerator AutoDepositCoin(Coin coin)
{
    yield return new WaitForSeconds(2f); // Wait 2 seconds
    if (_currentCoin == coin && _socketInteractor != null)
    {
        // Deposit the coin
        coin.OnDeposited();
        // Remove from socket
        _socketInteractor.UnselectInteractable(coin.GetComponent<IInteractableView>());
    }
}
```

## Events to Monitor

The CoinSlot script logs:
- When a coin snaps into the slot
- When a coin is released from the slot
- The denomination of each coin

You can expand the callbacks in CoinSlot.cs to trigger:
- Sounds (coin deposit chime)
- Animations (slot flash, etc.)
- Vending machine logic

## Troubleshooting

- **Coin not grabbing**: Ensure XR Grab Interactable is enabled and has interaction layers set
- **Coin not snapping**: Check Socket Interactor collider size and interaction layer mask
- **Coin passing through**: Verify Rigidbody collision detection is Continuous
