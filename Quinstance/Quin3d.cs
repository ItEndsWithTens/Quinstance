using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Quinstance
{
    namespace Quin3d
    {
        public class Point3d
        {
            private readonly double f_x, f_y, f_z, f_mag;

            public double x { get { return this.f_x; } }
            public double y { get { return this.f_y; } }
            public double z { get { return this.f_z; } }
            public double mag { get { return this.f_mag; } }

            public Point3d()
            {
                this.f_x = 0.0;
                this.f_y = 0.0;
                this.f_z = 0.0;
                this.f_mag = 0.0;
            }

            public Point3d(double x_in, double y_in, double z_in)
            {
                this.f_x = x_in;
                this.f_y = y_in;
                this.f_z = z_in;
                this.f_mag = System.Math.Sqrt(x_in * x_in + y_in * y_in + z_in * z_in);
            }

            public Point3d(Point3d vec_in)
            {
                this.f_x = vec_in.x;
                this.f_y = vec_in.y;
                this.f_z = vec_in.z;
                this.f_mag = vec_in.mag;
            }

            public double this[int col]
            {
                get
                {
                    if (col == 2)
                        return z;
                    else if (col == 1)
                        return y;
                    else
                        return x;
                }
            }

            public Point3d Cross(Point3d rhs)
            {
                return new Point3d(this.y * rhs.z - this.z * rhs.y,
                                   this.z * rhs.x - this.x * rhs.z,
                                   this.x * rhs.y - this.y * rhs.x);
            }

            public double Dot(Point3d rhs)
            {
                return this.x * rhs.x + this.y * rhs.y + this.z * rhs.z;
            }

            public static Point3d operator +(Point3d lhs, Point3d rhs)
            {
                return new Point3d(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z);
            }

            public static Point3d operator -(Point3d lhs, Point3d rhs)
            {
                return new Point3d(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z);
            }

            public Point3d Normalize()
            {
                return new Point3d(this.x / this.mag, this.y / this.mag, this.z / this.mag);
            }

            public override string ToString()
            {
                return "( " + this.x.ToString() + ' ' + this.y.ToString() + ' ' + this.z.ToString() + " )";
            }
        }

        public class Matrix3x3
        {
            // This class is implemented with a list of Point3ds in an attempt
            // at immutability, which I don't think I could achieve with only
            // an array of doubles.
            private readonly List<Point3d> rows = new List<Point3d>();

            public Matrix3x3()
            {
                rows.Add(new Point3d(1.0, 0.0, 0.0));
                rows.Add(new Point3d(0.0, 1.0, 0.0));
                rows.Add(new Point3d(0.0, 0.0, 1.0));
            }

            public Matrix3x3(double[] input)
            {
                rows.Add(new Point3d(input[0], input[1], input[2]));
                rows.Add(new Point3d(input[3], input[4], input[5]));
                rows.Add(new Point3d(input[6], input[7], input[8]));
            }

            public double this[int row, int col]
            {
                get { return rows[row][col]; }
            }
        }

        public class Plane
        {
            private readonly Point3d f_a, f_b, f_c, f_normal;

            public Point3d a { get { return this.f_a; } }
            public Point3d b { get { return this.f_b; } }
            public Point3d c { get { return this.f_c; } }
            public Point3d normal { get { return this.f_normal; } }

            public Plane(Plane p, bool flip_normal=false)
            {
                this.f_a = p.a;
                this.f_b = p.b;
                this.f_c = p.c;
                this.f_normal = p.normal;
                if (flip_normal)
                    this.f_normal = new Point3d(-this.f_normal.x, -this.f_normal.y, -this.f_normal.z);
            }

            public Plane(string map_line, bool flip_normal=false)
            {
                char[] plane_delims = { '(', ')', ' ' };
                string[] plane_strings = map_line.Split(plane_delims, StringSplitOptions.RemoveEmptyEntries);

                double a_x, a_y, a_z;
                Double.TryParse(plane_strings[0], out a_x);
                Double.TryParse(plane_strings[1], out a_y);
                Double.TryParse(plane_strings[2], out a_z);
                
                double b_x, b_y, b_z;
                Double.TryParse(plane_strings[3], out b_x);
                Double.TryParse(plane_strings[4], out b_y);
                Double.TryParse(plane_strings[5], out b_z);
                
                double c_x, c_y, c_z;
                Double.TryParse(plane_strings[6], out c_x);
                Double.TryParse(plane_strings[7], out c_y);
                Double.TryParse(plane_strings[8], out c_z);

                this.f_a = new Point3d(a_x, a_y, a_z);
                this.f_b = new Point3d(b_x, b_y, b_z);
                this.f_c = new Point3d(c_x, c_y, c_z);

                Point3d vec_a = new Point3d(this.b - this.a),
                        vec_b = new Point3d(this.c - this.a);
                this.f_normal = vec_a.Cross(vec_b).Normalize();
                if (flip_normal)
                    this.f_normal = new Point3d(-this.f_normal.x, -this.f_normal.y, -this.f_normal.z);
            }

            public override string ToString()
            {
                return this.a.ToString() + ' ' + this.b.ToString() + ' ' + this.c.ToString();
            }
        }

        public class Util
        {

            static double to_degrees = 180.0 / System.Math.PI;

            public static double UnsignedAngleBetweenVectors(Point3d lhs, Point3d rhs)
            {
                return System.Math.Acos(lhs.Normalize().Dot(rhs.Normalize())) * to_degrees;
            }

            public static Point3d MulMatrix3x3ByPoint3d(Matrix3x3 lhs, Point3d rhs)
            {
                double[] result = { 0.0, 0.0, 0.0 };

                for (int row = 0; row < 3; ++row)
                    result[row] = lhs[row, 0] * rhs[0] + lhs[row, 1] * rhs[1] + lhs[row, 2] * rhs[2];

                return new Point3d(result[0], result[1], result[2]);
            }
        }
    }
}
