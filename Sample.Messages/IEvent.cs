﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sample.Messages
{
    public interface IEvent
    {
        Guid Id { get; }
    }
}