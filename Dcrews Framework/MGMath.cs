using Dcrew.Framework;
using System;
using System.Collections.Generic;

namespace Microsoft.Xna.Framework
{
    public static class MGMath
    {
        public const float PI = (float)Math.PI;
        public const float TwoPI = (float)(Math.PI * 2);
        public const float HalfPI = (float)(Math.PI / 2);
        public const float PIOver4 = (float)(Math.PI / 4);
        public const float PIOver8 = (float)(Math.PI / 8);
        public const float PIOver16 = (float)(Math.PI / 16);
        public const float PIOver32 = (float)(Math.PI / 32);
        public const float PIOver64 = (float)(Math.PI / 64);
        public const float Rad2Deg = 57.295779513082320876798154814105f;
        public const float Deg2Rad = 0.017453292519943295769236907684886f;

        public static FastRandom Random;

        static readonly IDictionary<int, List<FastRandom>> _randoms = new Dictionary<int, List<FastRandom>>();

        static MGMath()
        {
            var random = new FastRandom();
            _randoms.Add(random.Seed, new List<FastRandom> { random });
            Random = random;
        }

        public static void SetRandomSeed(int seed, int instance = 0)
        {
            if (!_randoms.ContainsKey(seed))
            {
                List<FastRandom> instances;
                if ((instances = _randoms[seed]).Count == instance)
                    instances.Add(new FastRandom());
                else
                    throw new IndexOutOfRangeException($"There are only {instances.Count} instances, you must access them 1 after the last (0-based index) or any lower");
                Random = instances[instance];
                return;
            }
            Random = _randoms[seed][instance];
        }

        public static float Lerp(float source, float destination, float amount) => source + (destination - source) * amount;
        public static float LerpPrecise(float source, float destination, float amount) => ((1 - amount) * source) + (destination * amount);

        public static float WrapAngle(float angle)
        {
            if ((angle > -PI) && (angle <= PI))
                return angle;
            angle %= TwoPI;
            if (angle <= -PI)
                return angle + TwoPI;
            if (angle > PI)
                return angle - TwoPI;
            return angle;
        }
        public static float MoveAngle(float source, float destination, float speed)
        {
            float c, d;
            if (destination < source)
            {
                c = destination + TwoPI;
                d = c - source > source - destination ? (destination > source ? source + speed : source - speed) : (c > source ? source + speed : source - speed);
            }
            else if (destination > source)
            {
                c = destination - TwoPI;
                d = destination - source > source - c ? (c > source ? source + speed : source - speed) : (destination > source ? source + speed : source - speed);
            }
            else
                return source;
            return WrapAngle(d);
        }
        public static float MoveAngleReverse(float source, float destination, float speed)
        {
            float c, d;
            if (destination < source)
            {
                c = destination - TwoPI;
                d = destination - source > source - c ? (c > source ? source + speed : source - speed) : (destination > source ? source + speed : source - speed);
            }
            else if (destination > source)
            {
                c = destination + TwoPI;
                d = c - source > source - destination ? (destination > source ? source + speed : source - speed) : (c > source ? source + speed : source - speed);
            }
            else
                return source;
            return WrapAngle(d);
        }
        public static float LerpAngle(float source, float destination, float amount)
        {
            float c, d;
            if (destination < source)
            {
                c = destination + TwoPI;
                d = c - source > source - destination ? Lerp(source, destination, amount) : Lerp(source, c, amount);
            }
            else if (destination > source)
            {
                c = destination - TwoPI;
                d = destination - source > source - c ? Lerp(source, c, amount) : Lerp(source, destination, amount);
            }
            else
                return source;
            return WrapAngle(d);
        }
        public static float LerpAngleReverse(float source, float destination, float amount)
        {
            float c, d;
            if (destination < source)
            {
                c = destination - TwoPI;
                d = destination - source > source - c ? Lerp(source, c, amount) : Lerp(source, destination, amount);
            }
            else if (destination > source)
            {
                c = destination + TwoPI;
                d = c - source > source - destination ? Lerp(source, destination, amount) : Lerp(source, c, amount);
            }
            else
                return source;
            return WrapAngle(d);
        }
        public static float LerpAnglePrecise(float source, float destination, float amount)
        {
            float c, d;
            if (destination < source)
            {
                c = destination + TwoPI;
                d = c - source > source - destination ? LerpPrecise(source, destination, amount) : LerpPrecise(source, c, amount);
            }
            else if (destination > source)
            {
                c = destination - TwoPI;
                d = destination - source > source - c ? LerpPrecise(source, c, amount) : LerpPrecise(source, destination, amount);
            }
            else
                return source;
            return WrapAngle(d);
        }
        public static float LerpAngleReversePrecise(float source, float destination, float amount)
        {
            float c, d;
            if (destination < source)
            {
                c = destination - TwoPI;
                d = destination - source > source - c ? LerpPrecise(source, c, amount) : LerpPrecise(source, destination, amount);
            }
            else if (destination > source)
            {
                c = destination + TwoPI;
                d = c - source > source - destination ? LerpPrecise(source, destination, amount) : LerpPrecise(source, c, amount);
            }
            else
                return source;
            return WrapAngle(d);
        }
        public static int PackAngle(float radians, int bits)
        {
            var maxVal = 1 << bits;
            var packedRadians = (int)Math.Round((WrapAngle(radians) + PI) / TwoPI * maxVal);
            if (packedRadians == maxVal)
                packedRadians = 0;
            return packedRadians;
        }
        public static float UnpackAngle(int packedRadians, int bits) => packedRadians / (float)(1 << bits) * 360 * Deg2Rad - PI;
        public static Vector2 Rotate(Vector2 position, float angle) => Vector2.Transform(position, Matrix.CreateRotationZ(angle));
        public static Vector2 Rotate(Vector2 position, float angle, Vector2 origin) => Vector2.Transform((position - origin), Matrix.CreateRotationZ(angle)) + origin;
        public static float AngleDifference(float a, float b)
        {
            float aD = MathHelper.ToDegrees(a),
                bD = MathHelper.ToDegrees(b),
                difference = Math.Abs(aD - bD) % 360;
            if (difference > 180)
                difference = 360 - difference;
            difference *= Deg2Rad;
            return difference;
        }

        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Cos(float angle) => (float)Math.Cos(angle);
        public static float Sin(float angle) => (float)Math.Sin(angle);

        public static Vector2 Move(ref Vector2 position, float angle, float velocity) => position += new Vector2(Cos(angle) * velocity, Sin(angle) * velocity);
        public static Vector2 Move(ref Vector2 position, Vector2 other, float velocity) => Move(ref position, Atan2(other.Y - position.Y, other.X - position.X), velocity);
        public static Vector2 Move(Vector2 position, float angle, float velocity) => position + new Vector2(Cos(angle) * velocity, Sin(angle) * velocity);
        public static Vector2 Move(Vector2 position, Vector2 other, float velocity) => Move(position, Atan2(other.Y - position.Y, other.X - position.X), velocity);

        public static Vector2 FirstOrderIntercept((Vector2 Position, Vector2 Velocity) shooter, (Vector2 Position, Vector2 Velocity) target, float shotSpeedSqr, out float interceptTime)
        {
            Vector2 tRP = target.Position - shooter.Position,
                tRV = target.Velocity - shooter.Velocity;
            interceptTime = FirstOrderInterceptTime((tRP, tRV), shotSpeedSqr);
            return target.Position + (interceptTime * tRV);
        }
        public static float FirstOrderInterceptTime((Vector2 Position, Vector2 Velocity) shooter, (Vector2 Position, Vector2 Velocity) target, float shotSpeedSqr)
        {
            Vector2 tRP = target.Position - shooter.Position,
                tRV = target.Velocity - shooter.Velocity,
                vS = tRV * tRV;
            var a = tRV.LengthSquared() - shotSpeedSqr;
            if (Math.Abs(a) < .001f)
            {
                var t = -tRP.LengthSquared() / (2 * Vector2.Dot(tRV, tRP));
                return Math.Max(t, 0);
            }
            float b = 2 * Vector2.Dot(tRV, tRP),
                c = tRP.LengthSquared(),
                determinant = (b * b) - (4 * a * c);
            if (determinant > 0)
            {
                float t1 = (float)(-b + Math.Sqrt(determinant)) / (2 * a),
                    t2 = (float)(-b - Math.Sqrt(determinant)) / (2 * a);
                if (t1 > 0)
                    if (t2 > 0)
                        return Math.Min(t1, t2);
                    else
                        return t1;
                return Math.Max(t2, 0);
            }
            if (determinant < 0)
                return 0;
            return Math.Max(-b / (2 * a), 0);
        }
        public static float FirstOrderInterceptTime((Vector2 Position, Vector2 Velocity) targetRelative, float shotSpeedSqr)
        {
            var vS = targetRelative.Velocity * targetRelative.Velocity;
            var a = targetRelative.Velocity.LengthSquared() - shotSpeedSqr;
            if (Math.Abs(a) < .001f)
            {
                var t = -targetRelative.Position.LengthSquared() / (2 * Vector2.Dot(targetRelative.Velocity, targetRelative.Position));
                return Math.Max(t, 0);
            }
            float b = 2 * Vector2.Dot(targetRelative.Velocity, targetRelative.Position),
                c = targetRelative.Position.LengthSquared(),
                determinant = (b * b) - (4 * a * c);
            if (determinant > 0)
            {
                float t1 = (float)(-b + Math.Sqrt(determinant)) / (2 * a),
                    t2 = (float)(-b - Math.Sqrt(determinant)) / (2 * a);
                if (t1 > 0)
                    if (t2 > 0)
                        return Math.Min(t1, t2);
                    else
                        return t1;
                return Math.Max(t2, 0);
            }
            if (determinant < 0)
                return 0;
            return Math.Max(-b / (2 * a), 0);
        }
    }
}