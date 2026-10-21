namespace ProceduralGeneration.WorldGen.Density
{
    /// <summary>一维三次样条控制点：输入位置、输出值，以及对输入坐标的斜率。</summary>
    public readonly struct CubicSplinePoint
    {
        public readonly double Location;
        public readonly double Value;
        public readonly IDensityFunction ValueFunction;
        public readonly double Derivative;

        public CubicSplinePoint(double location, double value, double derivative = 0.0)
        {
            Location = location;
            Value = value;
            ValueFunction = null;
            Derivative = derivative;
        }

        /// <summary>嵌套样条控制点：控制点的输出由另一个 DensityFunction 求值。</summary>
        public CubicSplinePoint(double location, IDensityFunction valueFunction, double derivative = 0.0)
        {
            if (valueFunction == null) throw new System.ArgumentNullException(nameof(valueFunction));
            Location = location;
            Value = 0.0;
            ValueFunction = valueFunction;
            Derivative = derivative;
        }
    }
}
