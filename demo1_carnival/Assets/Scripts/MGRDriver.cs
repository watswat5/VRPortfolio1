using UnityEngine;

public class MGRDriver : MonoBehaviour
{
    public float degreesPerSecond = 90f;
    public float horseHeightRange = 0.5f; //How much the horses move up and down relative to their original position
    public float horseTiltRange = 15f; //How much the horses tilt forward and backward relative to their original rotation
    public GameObject[] horses;
    private float[] originalHorseY; //Used for offset calculations
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        originalHorseY = new float[horses.Length];

        for (int i = 0; i < horses.Length; i++)
        {
            originalHorseY[i] = horses[i].transform.localPosition.y;
        }
    }

    // Update is called once per frame
    void Update()
    {
        //Rotate this object around its local Y axis at the given rate
        transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime);

        //Sample a sin wave to get a value between 0 and 1 for each of the 8 horses.
        //Points should be equally spaced around the sin wave, so we can use the index of the horse to determine its position on the wave.
        //Effect should be that the horses move up and down in a wave pattern as the carousel rotates.
        for (int i = 0; i < horses.Length; i++)
        {
            //Offset each horse by pi/number of horses so that they are evenly spaced around the wave
            //Use this objects y rotation as the time value for the sin wave so that the horses move up and down as the carousel rotates
            //Should sync with speed
            float offset = (Mathf.PI * 2 / horses.Length) * i;
            float yOffset = Mathf.Sin(transform.localEulerAngles.y * Mathf.Deg2Rad + offset) * horseHeightRange;

            //Modify the horse's local position to move it up and down based on the sin wave value
            Vector3 horsePosition = horses[i].transform.localPosition;
            horsePosition.y = originalHorseY[i] + yOffset;
            horses[i].transform.localPosition = horsePosition;

            //Use the same sin wave value to tilt the horse forward and backward as it moves up and down
            float tilt = Mathf.Sin(transform.localEulerAngles.y * Mathf.Deg2Rad + offset) * horseTiltRange;
            Vector3 horseRotation = horses[i].transform.localEulerAngles;
            horseRotation.x = tilt;
            horses[i].transform.localEulerAngles = horseRotation;
        }
    }
}
