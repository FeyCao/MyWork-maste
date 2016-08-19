using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KGameServer
{
    public class UserOperation
    {
        public bool isBuy;
        public int index;
        public double price;

        public UserOperation(bool aIsBuy, int aIndex, double aPrice)
        {
            isBuy = aIsBuy;
            index = aIndex;
            price = aPrice;
        }
    }
}
