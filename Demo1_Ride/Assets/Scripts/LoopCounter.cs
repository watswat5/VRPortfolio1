using UnityEngine;

public class LoopCounter : MonoBehaviour
{
    public MGRDriver driver;
    //Reference to TMP text object
    public TMPro.TextMeshPro loopCounterText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //set text to "Loops: 0"
        if (loopCounterText != null)
        {
            loopCounterText.text = "Loops: 0";
        }
    }

    // Update is called once per frame
    void Update()
    {
        //Update loop text by taking current Time and dividing by the driver's angles/second value, then rounding down to the nearest whole number

        float secondsPerLoop = 360f / driver.degreesPerSecond;
        int loops = Mathf.FloorToInt(Time.time / secondsPerLoop);
        loopCounterText.text = $"Loops: {Mathf.Abs(loops)}";
    }
}
