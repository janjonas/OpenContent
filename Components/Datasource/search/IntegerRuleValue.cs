﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Satrabel.OpenContent.Components.Datasource.search
{
    public class IntegerRuleValue : RuleValue
    {
        private int Value;
        public IntegerRuleValue(int value)
        {
            Value = value;
        }
        public override int AsInteger
        {
            get
            {
                return Value;
            }
        }
        public override string AsString
        {
            get
            {
                return Value.ToString();
            }
        }
    }
}