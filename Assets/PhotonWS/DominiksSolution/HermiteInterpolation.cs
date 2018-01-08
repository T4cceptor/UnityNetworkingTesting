using UnityEngine;

public static class HermiteInterpolation
{
    /// <summary>
    /// performs a cubic hermite interpolation between startpoint/velocity and end point/velocity v2, where t in [0,1]
    /// </summary>
    public static Vector3 Interpolate(Vector3 startPoint, Vector3 startVelocity, Vector3 endPoint, Vector3 endVelocity, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        float hermite1 = 2 * t3 - 3 * t2 + 1;
        float hermite2 = -2 * t3 + 3 * t2;
        float hermite3 = t3 - 2 * t2 + t;
        float hermite4 = t3 - t2;
        return (hermite1 * startPoint + hermite2 * endPoint + hermite3 * startVelocity + hermite4 * endVelocity);
    }
}