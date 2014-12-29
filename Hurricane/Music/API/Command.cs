﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hurricane.Music.API
{
    class Command
    {
        public string Token { get; protected set; }
        public List<CommandBase> CommandActions { get; protected set; }

        public Command(string token, List<CommandBase> commandactions)
        {
            this.Token = token;
            this.CommandActions = commandactions;
        }
    }
}
