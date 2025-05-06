using System;

namespace Nalix.Helpers;

/// <summary>
/// Static helper class containing common mathematical operations not covert by <see cref="Math"/>.
/// </summary>
public static class MathHelper
{
    private const int _ANGLE_LOOKUP_SIZE = 1024; //has to be power of 2
    private static readonly float[] _SIN, _COS;

    /// <summary>
    /// Initializes the <see cref="MathHelper"/> class by initializing the angle lookup arrays for SIN and COS.
    /// </summary>
    static MathHelper()
    {
        _SIN = new float[_ANGLE_LOOKUP_SIZE];
        _COS = new float[_ANGLE_LOOKUP_SIZE];

        for (int i = 0; i < _ANGLE_LOOKUP_SIZE; i++)
        {
            _SIN[i] = (float)Math.Sin(i * Math.PI / _ANGLE_LOOKUP_SIZE * 2);
            _COS[i] = (float)Math.Cos(i * Math.PI / _ANGLE_LOOKUP_SIZE * 2);
        }
    }

    /// <summary>
    /// Maps an angle to a lookup index.
    /// </summary>
    /// <param name="angle">The angle to look up</param>
    /// <returns>The index closest to the provided angle</returns>
    private static int MapToLookupIndex(float angle)
        => (int)(angle * (_ANGLE_LOOKUP_SIZE / 360f) + 0.5f) & (_ANGLE_LOOKUP_SIZE - 1);

    /// <summary>
    /// Looks up a precalculated co-sinus value for the specified angle.
    /// </summary>
    /// <param name="angle">The angle to look up the according co-sinus.</param>
    /// <returns>Co-sinus closest to the provided angle.</returns>
    public static float Cos(float angle) => _COS[MapToLookupIndex(angle)];

    /// <summary>
    /// Looks up a precalculated sinus value for the specified angle.
    /// </summary>
    /// <param name="rotation">The angle to look up the according sinus.</param>
    /// <returns>Sinus closest to the provided angle.</returns>
    public static float Sin(float rotation) => _SIN[MapToLookupIndex(rotation)];

    /// <summary>
    /// Helper Method to map any angle to the usual range of [0 - 359.999999].
    /// </summary>
    /// <param name="angle">The angle to map</param>
    /// <returns>The mapped angle</returns>
    /// <example>MathHelper.ValidateAngle(380) => 20</example>
    public static float ValidateAngle(float angle) => (angle %= 360) < 0 ? angle + 360 : angle;

    /// <summary>
    /// Calculates the reflection angle from a ray hitting a surface.
    /// </summary>
    /// <param name="rayAngle">The ray angle.</param>
    /// <param name="surfaceAngle">The surface angle.</param>
    /// <returns>Angle of the reflected ray.</returns>
    public static float CalculateReflectionAngle(float rayAngle, float surfaceAngle)
        => ValidateAngle((360 - ValidateAngle(rayAngle)) + (2 * ValidateAngle(surfaceAngle)));

    /// <summary>
    /// Clamps the specified value to be withing the specified bounds.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum possible value.</param>
    /// <param name="max">The maximum possible value.</param>
    /// <returns>A value guarantied to be within the bounds specified</returns>
    public static float Clamp(float value, float min, float max)
        => value < min ? min : value > max ? max : value;

    /// <summary>
    /// Clamps the specified value to be withing the specified bounds.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum possible value.</param>
    /// <param name="max">The maximum possible value.</param>
    /// <returns>A value guarantied to be within the bounds specified</returns>
    public static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;
}
