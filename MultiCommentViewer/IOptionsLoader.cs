﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SitePlugin;
using MultiCommentViewer.Test;
using Common;
namespace MultiCommentViewer
{
    public interface IOptionsSerializer
    {
        IOptions Load(string path, IIo io);
        Task<IOptions> LoadAsync(string path, IIo io);
        Task WriteAsync(string path, IIo io, IOptions options);
    }
}
