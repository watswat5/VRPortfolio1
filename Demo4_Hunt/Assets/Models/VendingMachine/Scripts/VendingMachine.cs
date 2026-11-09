using System;
using System.Collections;
using System.ComponentModel;
using TMPro;
using UnityEngine;

namespace Scavenger.Vending
{
    /// <summary>Front is local +Z. Coils rotate around local Z; products stay upright.</summary>
    public sealed class VendingMachine : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Lane
        {
            public Transform screw;
            public Transform[] products;
            [HideInInspector] public int nextProduct;
            [HideInInspector] public Transform stuckProduct;
            public bool IsJammed => stuckProduct != null;
        }

        //Show credit is default
        // Show selection is entered on first button press and shows the digits as typed
        // Show selection times out to show credit after a few seconds
        // On the second button press, if credit is sufficient, the machine vends. Game complete!
        // Else, if credit is insufficient, the machine shows cost for a few seconds before returning to show credit
        private enum vendState{ show_credit, vend, show_selection, show_cost }
        private const int GridSize = 3;
        private vendState currentState;
        public Lane[] lanes;
        public Transform deliveryPoint;
        [Min(0.1f)] public float turnDuration = 1.4f;
        [Min(0.01f)] public float productPitch = 0.14f;
        [Min(0.1f)] public float dropDuration = 0.65f;

        // Track the buttons. They will inform the machine when pressed and what their ID is.
        // The machine uses these to either vend or to display the cost.
        public GameObject[] buttons;
        public int costPerVend = 100; //in cents
        //0 = Half dollar, 1 = quarter, 2 = dime, 3 = nickel
        public int[][] coins;

        private int[] digits;
        private int currentDigitIndex;
        public TextMeshPro costLabel;
        public TextMeshPro costDisplay;

        [Tooltip("Front pane collider, used to keep snacks inside the glass.")]
        public BoxCollider frontGlass;

        [Header("Starvation Ending")]
        [Tooltip("Uses the existing black death room. Found automatically when left empty.")]
        [SerializeField] private VRDeathRespawn endingController;
        [Min(0f)] [SerializeField] private float endingDelay = 3f;
        [SerializeField, TextArea] private string endingMessage =
            "You died of starvation in the desert. Nobody noticed.";

        public bool IsVending { get; private set; }
        private bool endingTriggered;

        void Awake()
        {
            digits = new int[2] {0, 0};
            currentDigitIndex = 0;
            currentState = vendState.show_credit;
            coins = new int[4][];
            //Set all coin counts to 0
            for(int i = 0; i < coins.Length; i++)
            {
                coins[i] = new int[1] {0};
            }

            if (endingController == null)
                endingController = FindFirstObjectByType<VRDeathRespawn>();
        }
        /// <summary>Call from a UI button, XR event, or gameplay script. Lane numbers start at zero.</summary>
        public void Vend(int laneIndex) => Vend(laneIndex, false);

        /// <summary>One-based row/column: (1,1) is A1, (3,3) is C3.</summary>
        public void Vend(int row, int column, bool getStuck = false)
        {
            if (!TryGetLaneIndex(row, column, out int laneIndex))
            {
                Debug.LogWarning($"Rejected out-of-bounds vending selection ({row}, {column}).", this);
                return;
            }

            if (TryVendLane(laneIndex, getStuck))
                SubtractCredit(costPerVend);
        }

        /// <summary>When getStuck is true, the snack tips against the glass and blocks this lane.</summary>
        public void Vend(int laneIndex, bool getStuck) => TryVendLane(laneIndex, getStuck);

        private bool TryVendLane(int laneIndex, bool getStuck)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsVending || lanes == null ||
                laneIndex < 0 || laneIndex >= lanes.Length || deliveryPoint == null) return false;
            Lane lane = lanes[laneIndex];
            if (lane == null || lane.IsJammed || lane.screw == null || lane.products == null) return false;
            while (lane.nextProduct < lane.products.Length && lane.products[lane.nextProduct] == null)
                lane.nextProduct++;
            if (lane.nextProduct >= lane.products.Length) return false;
            StartCoroutine(VendRoutine(lane, getStuck));
            return true;
        }

        private bool TryGetLaneIndex(int row, int column, out int laneIndex)
        {
            laneIndex = -1;
            if (row < 1 || row > GridSize || column < 1 || column > GridSize)
                return false;

            int candidate = (row - 1) * GridSize + column - 1;
            if (lanes == null || candidate >= lanes.Length)
                return false;

            laneIndex = candidate;
            return true;
        }

        [ContextMenu("Vend first lane (Play Mode)")]
        public void VendFirstLane() => Vend(0);

        [ContextMenu("Jam first lane (Play Mode)")]
        public void JamFirstLane() => Vend(0, true);

        private IEnumerator VendRoutine(Lane lane, bool getStuck)
        {
            IsVending = true;
            int front = lane.nextProduct;
            Quaternion rotation = lane.screw.localRotation;
            var starts = new Vector3[lane.products.Length];
            for (int i = front; i < starts.Length; i++)
                if (lane.products[i]) starts[i] = lane.products[i].localPosition;

            float elapsed = 0f;
            while (elapsed < turnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(0.1f, turnDuration)));
                // Positive helix winding moves forward when rotated negatively.
                lane.screw.localRotation = rotation * Quaternion.AngleAxis(-360f * t, Vector3.forward);
                for (int i = front; i < starts.Length; i++)
                    if (lane.products[i]) lane.products[i].localPosition = starts[i] + Vector3.forward * (productPitch * t);
                yield return null;
            }
            lane.screw.localRotation = rotation;
            Transform product = lane.products[front];
            lane.nextProduct++;
            if (product)
            {
                if (getStuck)
                {
                    // Mark the item before animating so interruption cannot lose the jam state.
                    lane.stuckProduct = product;
                    yield return JamRoutine(product);
                    IsVending = false;
                    TriggerStarvationEnding();
                    yield break;
                }
                Vector3 start = transform.InverseTransformPoint(product.position);
                Vector3 end = transform.InverseTransformPoint(deliveryPoint.position);
                Quaternion startRotation = product.rotation;
                elapsed = 0;
                while (elapsed < dropDuration && product)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, dropDuration));
                    // Travel through the open front channel, beyond the shelf edges.
                    Vector3 point = Vector3.Lerp(start, end, t);
                    point.y = Mathf.Lerp(start.y, end.y, t * t);
                    point.z = Mathf.Lerp(start.z, end.z, Mathf.Min(t * 4, 1));
                    product.position = transform.TransformPoint(point);
                    product.rotation = startRotation * Quaternion.Euler(65 * t, 15 * t, 10 * t);
                    KeepInsideGlass(product);
                    yield return null;
                }
                if (product)
                {
                    product.SetParent(null, true);
                    // The delivered snack is a normal collider-bearing object, ready for your grab logic.
                    product.gameObject.name = "VendedSnack";
                }
            }
            IsVending = false;
        }

        private void TriggerStarvationEnding()
        {
            if (endingTriggered)
                return;

            if (endingController == null)
                endingController = FindFirstObjectByType<VRDeathRespawn>();

            if (endingController == null)
            {
                Debug.LogWarning("Vending machine could not find VRDeathRespawn for the starvation ending.", this);
                return;
            }

            endingTriggered = true;
            endingController.EndGame(endingMessage, endingDelay);
        }

        private IEnumerator JamRoutine(Transform product)
        {
            Vector3 start = transform.InverseTransformPoint(product.position);
            Quaternion startRotation = Quaternion.Inverse(transform.rotation) * product.rotation;
            Bounds bounds = ProductBounds(product);
            // Tip around the bottom rear edge, leaving the lower end caught near the spiral.
            Vector3 pivot = new Vector3(start.x, bounds.min.y, bounds.min.z);
            float elapsed = 0;
            float duration = Mathf.Max(.1f, dropDuration);
            while (elapsed < duration && product)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float fall = Mathf.Clamp01(t / .72f);
                float rebound = Mathf.Clamp01((t - .72f) / .28f);
                float angle = 22 * fall * fall - 3 * Mathf.Sin(rebound * Mathf.PI) * (1 - rebound);
                Quaternion tip = Quaternion.AngleAxis(angle, Vector3.right);
                product.position = transform.TransformPoint(pivot + tip * (start - pivot));
                product.rotation = transform.rotation * tip * startRotation;
                KeepInsideGlass(product);
                yield return null;
            }
            if (product) product.name = "StuckSnack";
        }

        private Bounds ProductBounds(Transform product)
        {
            var collider = product.GetComponent<BoxCollider>();
            // Matches the supplied snack; custom snacks should keep a BoxCollider.
            Vector3 center = collider ? collider.center : Vector3.zero;
            Vector3 half = collider ? collider.size * .5f : new Vector3(.085f, .115f, .0275f);
            var bounds = new Bounds(transform.InverseTransformPoint(product.TransformPoint(center)), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = center + Vector3.Scale(half, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                bounds.Encapsulate(transform.InverseTransformPoint(product.TransformPoint(corner)));
            }
            return bounds;
        }

        private void KeepInsideGlass(Transform product)
        {
            // Fallback preserves the jam behavior on prefab instances made before the pane was added.
            float glassZ = .36f, glassBottom = .495f;
            if (frontGlass)
            {
                Vector3 innerBottom = transform.InverseTransformPoint(frontGlass.transform.TransformPoint(frontGlass.center - frontGlass.size * .5f));
                glassZ = innerBottom.z;
                glassBottom = innerBottom.y;
            }
            Bounds bounds = ProductBounds(product);
            if (bounds.max.y < glassBottom) return;
            float overlap = bounds.max.z - (glassZ - .002f);
            if (overlap > 0)
            {
                Vector3 position = transform.InverseTransformPoint(product.position);
                position.z -= overlap;
                product.position = transform.TransformPoint(position);
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            IsVending = false;
        }

        public void PressButton(int number)
        {
            if (number < 1 || number > GridSize)
            {
                Debug.LogWarning($"Rejected out-of-bounds vending button value {number}.", this);
                return;
            }

            // Track two button presses at a time
            // On second button press, evaluate state change and reset buffer.
            if (currentDigitIndex != 0) //Second press
            {
                // Second button press, don't need to change state, just evaluate and reset buffer.
                digits[currentDigitIndex] = number;
                currentDigitIndex = 0;
                if(GetCredit() < costPerVend)
                {
                    SetState(vendState.show_cost);
                    UpdateSelection();
                } else {
                    Vend(digits[0], digits[1], true);
                }
            } else //First press
            {
                // First button press, change state to show selection and store the digit.
                digits[currentDigitIndex] = number;
                currentDigitIndex = 1;
                SetState(vendState.show_selection);
            }
        }

        private void SetState(vendState newState)
        {
            currentState = newState;
            switch (currentState)
            {
                case vendState.show_credit:
                    costLabel.text = "Credit";
                    costDisplay.text = $"${GetCredit() / 100}.{GetCredit() % 100:D2}";
                    break;
                case vendState.show_selection:
                    StartCoroutine(ShowDigitsRoutine());
                    costLabel.text = "Selection";
                    costDisplay.text = $"{digits[0]}{(digits[1] == 0 ? "_" : digits[1])}";
                    break;
                case vendState.show_cost:
                    StartCoroutine(ShowCostRoutine());
                    break;
                case vendState.vend:
                    // Vend is handled by the button press logic.
                    break;
            }
        }
        private IEnumerator ShowCostRoutine()
        {
            // Show the cost of the selected item for a few seconds, then return to showing credit.
            //Set the text of the cost display to the cost of the selected item
            costLabel.text = "Cost";
            costDisplay.text = $"${costPerVend / 100}.{costPerVend % 100:D2}";

            //Wait, and set state to show credit
            yield return new WaitForSeconds(3f);
            SetState(vendState.show_credit);
            yield return null;
        }

        private IEnumerator ShowDigitsRoutine()
        {
            yield return new WaitForSeconds(3f);
            //Clear buffer and reset pointer
            digits[0] = 0;
            digits[1] = 0;
            currentDigitIndex = 0;
            //Guard against changing state if the user has already moved on to another state
            if (currentState == vendState.show_selection)
                SetState(vendState.show_credit);
            yield return null;
        }

        private void UpdateSelection()
        {
            if(currentState == vendState.show_selection)
            {
                //A 0 is invalid and means the user has not pressed a button for that digit yet, so we don't want to display it.
                //Display a space instead.
                costLabel.text = "Selection";
                costDisplay.text = $"${(digits[0] == 0 ? " " : digits[0].ToString())}{(digits[1] == 0 ? " " : digits[1].ToString())}";

                if(GetCredit() >= costPerVend)
                {
                    //If the user has enough credit, vend the item and reset the selection.
                    Vend(digits[0], digits[1], true);
                    digits[0] = 0;
                    digits[1] = 0;
                    SetState(vendState.show_credit);
                }
                else
                {
                    //If the user does not have enough credit, show the cost of the item for a few seconds, then return to showing credit.
                    SetState(vendState.show_cost);
                }
            }
        }

        public void AddCredit(int coinIndex)
        {
            if(coinIndex < 0 || coinIndex >= coins.Length)
            {
                Debug.LogWarning("Invalid coin index: " + coinIndex);
                return;
            }
            coins[coinIndex][0]++;
            if(currentState == vendState.show_credit)
            {
                costDisplay.text = $"${GetCredit() / 100}.{GetCredit() % 100:D2}";
            }
        }
        
        private int GetCredit()
        {
            int t_credit = 0;
            for(int i = 0; i < coins.Length; i++)
            {
                t_credit += coins[i][0] * (i == 0 ? 50 : i == 1 ? 25 : i == 2 ? 10 : 5);
            }
            return t_credit;
        }

        private void SubtractCredit(int amount)
        {
            int credit = GetCredit();
            if(amount > credit)
            {
                Debug.LogWarning("Attempted to subtract more credit than available.");
                return;
            }
            credit -= amount;
            //Reset all coin counts to 0
            for(int i = 0; i < coins.Length; i++)
            {
                coins[i][0] = 0;
            }
            //Recalculate coin counts based on remaining credit
            for(int i = 0; i < coins.Length; i++)
            {
                int coinValue = (i == 0 ? 50 : i == 1 ? 25 : i == 2 ? 10 : 5);
                coins[i][0] = credit / coinValue;
                credit -= coins[i][0] * coinValue;
            }
        }
    }
}
