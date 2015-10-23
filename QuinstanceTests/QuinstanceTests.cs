using NUnit.Framework;
using Quinstance;
using Quinstance.Quin3d;
using System;

namespace QuinstanceTests
{
    [TestFixture]
    public class StripCommentTests
    {
        [TestCase]
        public void StripComment_RemovesCommentAtStartOfLine()
        {
            string actual = Quinstance.Util.StripComment("// StripComment testing");
            Assert.That(actual, Is.EqualTo(""));
        }

        [TestCase]
        public void StripComment_RemovesCommentWithLeadingWhitespace()
        {
            string actual = Quinstance.Util.StripComment("    // StripComment testing");
            Assert.That(actual, Is.EqualTo("    "));
        }

        [TestCase]
        public void StripComment_RemovesCommentAfterFgdText()
        {
            string actual = Quinstance.Util.StripComment("@SolidClass = worldspawn : \"World entity\" // StripComment testing");
            Assert.That(actual, Is.EqualTo("@SolidClass = worldspawn : \"World entity\" "));
        }
    }

    [TestFixture]
    public class MathTests
    {
        [TestCase]
        public void Point3d_Cross_123_456()
        {
            Point3d a = new Point3d(1.0, 2.0, 3.0),
                    b = new Point3d(4.0, 5.0, 6.0);
            Point3d actual = a.Cross(b);
            Point3d expected = new Point3d(-3.0, 6.0, -3.0);

            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1).Ulps);
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1).Ulps);
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(1).Ulps);
        }

        [TestCase]
        public void Point3d_Cross_Oddball()
        {
            // I started with random numbers for a and b, got the values of
            // 'expected', then used the Visual Studio debugger's Watch panel
            // to get (double)(float)value. This test case will therefore serve
            // only as a regression test in case I do something stupid.
            Point3d a = new Point3d(0.001, 0.0001, 0.00001),
                 b = new Point3d(473.6, 9.0, 1.1);
            Point3d actual = a.Cross(b);
            Point3d expected = new Point3d(0.000020000000000000012, 0.0036360000000000003, -0.038360000000000005);

            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1).Ulps);
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1).Ulps);
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(1).Ulps);
        }

        [TestCase]
        public void Point3d_Dot_123_456()
        {
            Point3d a = new Point3d(1.0, 2.0, 3.0),
                 b = new Point3d(4.0, 5.0, 6.0);
            double actual = a.Dot(b);
            double expected = 32.0;

            Assert.That(actual, Is.EqualTo(expected).Within(1).Ulps);
        }

        [TestCase]
        public void Point3d_Dot_Oddball()
        {
            Point3d a = new Point3d(0.001, 0.0001, 0.00001),
                 b = new Point3d(473.6, 9.0, 1.1);
            double actual = a.Dot(b);
            double expected = 0.474511;

            Assert.That(actual, Is.EqualTo(expected).Within(1).Ulps);
        }

        [TestCase]
        public void Point3d_Subtract_123()
        {
            Point3d a = new Point3d(1.0, 2.0, 3.0),
                 b = new Point3d(1.0, 2.0, 3.0);
            Point3d actual = a - b;
            Point3d expected = new Point3d(0.0, 0.0, 0.0);

            Assert.That(actual.x, Is.EqualTo(expected.x).Within(1).Ulps);
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(1).Ulps);
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(1).Ulps);
        }
    }

    [TestFixture]
    public class Vector_AlongX_Rotated_90
    {
        Point3d point_to_rotate = new Point3d(1.0, 0.0, 0.0);

        static double angle = 90.0;

        static double cos = Math.Cos(angle * (System.Math.PI / 180.0)),
                      sin = Math.Sin(angle * (System.Math.PI / 180.0));

        Matrix3x3 matrix_x = new Matrix3x3(new double[] { 1.0,  0.0,  0.0,
                                                          0.0,  cos, -sin,
                                                          0.0,  sin,  cos }),

                  matrix_y = new Matrix3x3(new double[] { cos,  0.0,  sin,
                                                          0.0,  1.0,  0.0,
                                                         -sin,  0.0,  cos }),
                  
                  matrix_z = new Matrix3x3(new double[] { cos, -sin,  0.0,
                                                          sin,  cos,  0.0,
                                                          0.0,  0.0,  1.0 });

        [TestCase]
        public void Vector_AlongX_Rotated_90_AroundX()
        {
            Point3d rotated_around_x = Quinstance.Quin3d.Util.MulMatrix3x3ByPoint3d(matrix_x, point_to_rotate);

            Assert.That(rotated_around_x.x, Is.EqualTo(1.0).Within(0.0001));
            Assert.That(rotated_around_x.y, Is.EqualTo(0.0).Within(0.0001));
            Assert.That(rotated_around_x.z, Is.EqualTo(0.0).Within(0.0001));
        }

        [TestCase]
        public void Vector_AlongX_Rotated_90_AroundY()
        {
            Point3d rotated_around_y = Quinstance.Quin3d.Util.MulMatrix3x3ByPoint3d(matrix_y, point_to_rotate);

            Assert.That(rotated_around_y.x, Is.EqualTo(0.0).Within(0.0001));
            Assert.That(rotated_around_y.y, Is.EqualTo(0.0).Within(0.0001));
            Assert.That(rotated_around_y.z, Is.EqualTo(-1.0).Within(0.0001));
        }

        [TestCase]
        public void Vector_AlongX_Rotated_90_AroundZ()
        {
            Point3d rotated_around_z = Quinstance.Quin3d.Util.MulMatrix3x3ByPoint3d(matrix_z, point_to_rotate);

            Assert.That(rotated_around_z.x, Is.EqualTo(0.0).Within(0.0001));
            Assert.That(rotated_around_z.y, Is.EqualTo(1.0).Within(0.0001));
            Assert.That(rotated_around_z.z, Is.EqualTo(0.0).Within(0.0001));
        }
    }

    [TestFixture]
    public class Vector_AlongX_Rotated_45
    {
        Point3d point_to_rotate = new Point3d(1.0, 0.0, 0.0);

        static double angle = 45.0;

        static double cos = Math.Cos(angle * (System.Math.PI / 180.0)),
                      sin = Math.Sin(angle * (System.Math.PI / 180.0));

        Matrix3x3 matrix_x = new Matrix3x3(new double[] { 1.0,  0.0,  0.0,
                                                          0.0,  cos, -sin,
                                                          0.0,  sin,  cos }),

                  matrix_y = new Matrix3x3(new double[] { cos,  0.0,  sin,
                                                          0.0,  1.0,  0.0,
                                                         -sin,  0.0,  cos }),

                  matrix_z = new Matrix3x3(new double[] { cos, -sin,  0.0,
                                                          sin,  cos,  0.0,
                                                          0.0,  0.0,  1.0 });

        [TestCase]
        public void Vector_AlongX_Rotated_45_AroundX()
        {
            Point3d rotated_around_x = Quinstance.Quin3d.Util.MulMatrix3x3ByPoint3d(matrix_x, point_to_rotate);

            Assert.That(rotated_around_x.x, Is.EqualTo(1.0).Within(0.0001));
            Assert.That(rotated_around_x.y, Is.EqualTo(0.0).Within(0.0001));
            Assert.That(rotated_around_x.z, Is.EqualTo(0.0).Within(0.0001));
        }

        [TestCase]
        public void Vector_AlongX_Rotated_45_AroundY()
        {
            Point3d rotated_around_y = Quinstance.Quin3d.Util.MulMatrix3x3ByPoint3d(matrix_y, point_to_rotate);

            Assert.That(rotated_around_y.x, Is.EqualTo(0.707107).Within(0.0001));
            Assert.That(rotated_around_y.y, Is.EqualTo(0.0).Within(0.0001));
            Assert.That(rotated_around_y.z, Is.EqualTo(-0.707107).Within(0.0001));
        }

        [TestCase]
        public void Vector_AlongX_Rotated_45_AroundZ()
        {
            Point3d rotated_around_z = Quinstance.Quin3d.Util.MulMatrix3x3ByPoint3d(matrix_z, point_to_rotate);

            Assert.That(rotated_around_z.x, Is.EqualTo(0.707107).Within(0.0001));
            Assert.That(rotated_around_z.y, Is.EqualTo(0.707107).Within(0.0001));
            Assert.That(rotated_around_z.z, Is.EqualTo(0.0).Within(0.0001));
        }
    }

    [TestFixture]
    public class Vector_AlongX_Rotated_27
    {
        Point3d point_to_rotate = new Point3d(1.0, 0.0, 0.0);

        static double angle = 27.0;

        static double cos = Math.Cos(angle * (System.Math.PI / 180.0)),
                      sin = Math.Sin(angle * (System.Math.PI / 180.0));

        Matrix3x3 matrix_x = new Matrix3x3(new double[] { 1.0,  0.0,  0.0,
                                                          0.0,  cos, -sin,
                                                          0.0,  sin,  cos }),

                  matrix_y = new Matrix3x3(new double[] { cos,  0.0,  sin,
                                                          0.0,  1.0,  0.0,
                                                         -sin,  0.0,  cos }),

                  matrix_z = new Matrix3x3(new double[] { cos, -sin,  0.0,
                                                          sin,  cos,  0.0,
                                                          0.0,  0.0,  1.0 });

        [TestCase]
        public void Vector_AlongX_Rotated_27_AroundX()
        {
            Point3d rotated_around_x = Quinstance.Quin3d.Util.MulMatrix3x3ByPoint3d(matrix_x, point_to_rotate);

            Assert.That(rotated_around_x.x, Is.EqualTo(1.0).Within(0.0001));
            Assert.That(rotated_around_x.y, Is.EqualTo(0.0).Within(0.0001));
            Assert.That(rotated_around_x.z, Is.EqualTo(0.0).Within(0.0001));
        }

        [TestCase]
        public void Vector_AlongX_Rotated_27_AroundY()
        {
            Point3d rotated_around_y = Quinstance.Quin3d.Util.MulMatrix3x3ByPoint3d(matrix_y, point_to_rotate);

            Assert.That(rotated_around_y.x, Is.EqualTo(0.891007).Within(0.0001));
            Assert.That(rotated_around_y.y, Is.EqualTo(0.0).Within(0.0001));
            Assert.That(rotated_around_y.z, Is.EqualTo(-0.45399).Within(0.0001));
        }

        [TestCase]
        public void Vector_AlongX_Rotated_27_AroundZ()
        {
            Point3d rotated_around_z = Quinstance.Quin3d.Util.MulMatrix3x3ByPoint3d(matrix_z, point_to_rotate);

            Assert.That(rotated_around_z.x, Is.EqualTo(0.891007).Within(0.0001));
            Assert.That(rotated_around_z.y, Is.EqualTo(0.45399).Within(0.0001));
            Assert.That(rotated_around_z.z, Is.EqualTo(0.0).Within(0.0001));
        }
    }

    [TestFixture]
    public class Vector_AlongY_Rotated_27
    {
        Point3d point_to_rotate = new Point3d(0.0, 1.0, 0.0);

        static double angle = 27.0;

        static double cos = Math.Cos(angle * (System.Math.PI / 180.0)),
                      sin = Math.Sin(angle * (System.Math.PI / 180.0));

        Matrix3x3 matrix_x = new Matrix3x3(new double[] { 1.0,  0.0,  0.0,
                                                          0.0,  cos, -sin,
                                                          0.0,  sin,  cos }),

                  matrix_y = new Matrix3x3(new double[] { cos,  0.0,  sin,
                                                          0.0,  1.0,  0.0,
                                                         -sin,  0.0,  cos }),

                  matrix_z = new Matrix3x3(new double[] { cos, -sin,  0.0,
                                                          sin,  cos,  0.0,
                                                          0.0,  0.0,  1.0 });

        [TestCase]
        public void Vector_AlongY_Rotated_27_AroundX()
        {
            Point3d rotated_around_x = Quinstance.Quin3d.Util.MulMatrix3x3ByPoint3d(matrix_x, point_to_rotate);

            Assert.That(rotated_around_x.x, Is.EqualTo(0.0).Within(0.0001));
            Assert.That(rotated_around_x.y, Is.EqualTo(0.891007).Within(0.0001));
            Assert.That(rotated_around_x.z, Is.EqualTo(0.45399).Within(0.0001));
        }

        [TestCase]
        public void Vector_AlongY_Rotated_27_AroundY()
        {
            Point3d rotated_around_y = Quinstance.Quin3d.Util.MulMatrix3x3ByPoint3d(matrix_y, point_to_rotate);

            Assert.That(rotated_around_y.x, Is.EqualTo(0.0).Within(0.0001));
            Assert.That(rotated_around_y.y, Is.EqualTo(1.0).Within(0.0001));
            Assert.That(rotated_around_y.z, Is.EqualTo(0.0).Within(0.0001));
        }

        [TestCase]
        public void Vector_AlongY_Rotated_27_AroundZ()
        {
            Point3d rotated_around_z = Quinstance.Quin3d.Util.MulMatrix3x3ByPoint3d(matrix_z, point_to_rotate);

            Assert.That(rotated_around_z.x, Is.EqualTo(-0.45399).Within(0.0001));
            Assert.That(rotated_around_z.y, Is.EqualTo(0.891007).Within(0.0001));
            Assert.That(rotated_around_z.z, Is.EqualTo(0.0).Within(0.0001));
        }
    }
}
