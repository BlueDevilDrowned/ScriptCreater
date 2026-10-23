using System;
namespace ProceduralGeneration.WorldGen.Density
{
    internal static class OriginalTerrainProvider
    {
        public static IDensityFunction PeaksAndValleys(IDensityFunction r)
        {
            var inner = DensityFunctions.Abs(DensityFunctions.Subtract(DensityFunctions.Abs(r), DensityFunctions.Constant(2.0 / 3.0)));
            return DensityFunctions.Multiply(DensityFunctions.Constant(-3), DensityFunctions.Subtract(inner, DensityFunctions.Constant(1.0 / 3.0)));
        }
        public static IDensityFunction Offset(IDensityFunction c, IDensityFunction e, IDensityFunction p)
        {
            var a=ErosionOffset(e,p,-.15,0,0); var b=ErosionOffset(e,p,-.10,.03,.10); var d=ErosionOffset(e,p,-.10,.03,.70); var f=ErosionOffset(e,p,-.05,.03,1);
            return DensityFunctions.Spline(c,new CubicSplinePoint(-1.10,.044),new CubicSplinePoint(-1.02,-.2222),new CubicSplinePoint(-.51,-.2222),new CubicSplinePoint(-.44,-.12),new CubicSplinePoint(-.18,-.12),new CubicSplinePoint(-.16,a),new CubicSplinePoint(-.10,b),new CubicSplinePoint(.25,d),new CubicSplinePoint(1,f));
        }
        public static IDensityFunction Factor(IDensityFunction c, IDensityFunction e, IDensityFunction p)
        { return DensityFunctions.Spline(c,new CubicSplinePoint(-.19,3.95),new CubicSplinePoint(-.15,ErosionFactor(e,p,6.25,true)),new CubicSplinePoint(-.10,ErosionFactor(e,p,5.47,true)),new CubicSplinePoint(.03,ErosionFactor(e,p,5.08,true)),new CubicSplinePoint(.06,ErosionFactor(e,p,4.69,false))); }
        public static IDensityFunction Jaggedness(IDensityFunction c, IDensityFunction e, IDensityFunction p)
        { var ridge=DensityFunctions.Spline(p,new CubicSplinePoint(-1,0),new CubicSplinePoint(-.4,.5),new CubicSplinePoint(0,0),new CubicSplinePoint(.4,1),new CubicSplinePoint(1,0)); var er=DensityFunctions.Spline(e,new CubicSplinePoint(-1,1),new CubicSplinePoint(-.5775,1),new CubicSplinePoint(-.375,0)); return DensityFunctions.Spline(c,new CubicSplinePoint(-.11,0),new CubicSplinePoint(.03,DensityFunctions.Multiply(ridge,er)),new CubicSplinePoint(.65,ridge)); }
        private static IDensityFunction ErosionOffset(IDensityFunction e,IDensityFunction p,double a,double b,double c)
        { var r=DensityFunctions.Spline(p,new CubicSplinePoint(-1,a),new CubicSplinePoint(-.4,b),new CubicSplinePoint(0,c),new CubicSplinePoint(.4,c+.07),new CubicSplinePoint(1,c)); return DensityFunctions.Spline(e,new CubicSplinePoint(-.85,r),new CubicSplinePoint(-.70,r),new CubicSplinePoint(-.40,r),new CubicSplinePoint(-.35,r),new CubicSplinePoint(-.10,r),new CubicSplinePoint(.20,r),new CubicSplinePoint(.70,r)); }
        private static IDensityFunction ErosionFactor(IDensityFunction e,IDensityFunction p,double scale,bool mountain)
        { var broad=DensityFunctions.Spline(e,new CubicSplinePoint(-.2,6.3),new CubicSplinePoint(.2,scale)); var r=DensityFunctions.Spline(p,new CubicSplinePoint(-1,scale),new CubicSplinePoint(-.4,scale),new CubicSplinePoint(0,scale),new CubicSplinePoint(.4,mountain ? .625 : 1.37),new CubicSplinePoint(1,scale)); return DensityFunctions.Spline(e,new CubicSplinePoint(-.6,broad),new CubicSplinePoint(-.5,r),new CubicSplinePoint(-.35,broad),new CubicSplinePoint(-.25,broad),new CubicSplinePoint(-.1,r),new CubicSplinePoint(.03,broad),new CubicSplinePoint(.58,r)); }
    }
}

