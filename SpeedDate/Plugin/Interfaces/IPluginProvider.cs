﻿using System.Collections.Generic;

namespace SpeedDate.Plugin.Interfaces
{
    public interface IPluginProvider
    {
        void RegisterPlugin(IPlugin plugin);

        T Get<T>() where T: class, IPlugin;

        IEnumerable<IPlugin> GetAll();

        void Clear();
    }
}