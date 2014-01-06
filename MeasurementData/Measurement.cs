using System;

namespace MeasurementData
{
    public class Measurement
    {
        public float O2Value { get; set; }
        public float CO2Value { get; set; }
        public DateTime Timestamp { get; set; }

        public Measurement()
            : this(0, 0, DateTime.Now)
        {
        }

        public Measurement(float o2, float co2) 
            : this(o2, co2, DateTime.Now)
        {
        }

        public Measurement(float o2, float co2, DateTime stamp)
        {
            this.O2Value = o2;
            this.CO2Value = co2;
            this.Timestamp = stamp;
        }
    }
}
