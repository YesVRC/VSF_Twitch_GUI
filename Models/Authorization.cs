using System;
using System.Collections.Generic;
using System.Text;

namespace VSF_Twitch_GUI.Models
{
    public class Authorization
    {
        public string Code { get; }

        public Authorization(string code)
        {
            Code = code;
        }
    }
}