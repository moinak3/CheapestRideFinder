using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TestBotMoinak
{
    public class CabProvider
    {
        public string Name { get; set; }
        public double basePrice { get; set; }
        public double perminute { get; set; }
        public double perkm { get; set; }
        public double includedkm { get; set; }
        public double includedpermin { get; set; }
        public double minfare { get; set; }
        public CabProvider(string name, double basePrice, double perminute, double includedkm, double includedpermin, double perkm,
            double minfare)
        {
            this.Name = name;
            this.basePrice = basePrice;
            this.perminute = perminute;
            this.perkm = perkm;
            this.includedkm = includedkm;
            this.includedpermin = includedpermin;
            this.minfare = minfare;
        }

        public CabProvider()
        {

        }

        public double Overage(double total, double included)
        {
            if ((total - included) <= 0)
            {
                return 0.0;
            }
            else
            {
                return (total - included);
            }
        }

        public double GetTotalRate(string name, double distance, double duration, double minFare)
        {
            double total = 0.0;
            double distanceTotal = 0.0;
            double durationTotal = 0.0;

            distanceTotal = (Overage(distance, includedkm) * perkm); //Distance travelled over and above the included KM quantity multiplied by the per KM rate
            durationTotal = (Overage(duration, includedpermin) * perminute);

            total = basePrice + distanceTotal + durationTotal;

            if (total < minFare)
            {
                total = minFare;
            }
            return Math.Round(total, 2);
        }
    }
}