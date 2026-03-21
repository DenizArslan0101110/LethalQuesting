using UnityEngine;

namespace LethalQuesting.Utils
{
    public class MathUtils
    {
        // returns int because we never use this to get a float
        public static int SimpleBellCurve(float min, float max)
        {
            float r1 = UnityEngine.Random.Range(min, max);
            float r2 = UnityEngine.Random.Range(min, max);
            float r3 = UnityEngine.Random.Range(min, max);
    
            return Mathf.RoundToInt((r1 + r2 + r3) / 3f);
        }
        
        // a number string returns as one number above
        public static string IncrementStringNumber(string input)
        {
            if (int.TryParse(input, out int number))
            {
                return (number + 1).ToString();
            }
            return input;
        }
    }
}