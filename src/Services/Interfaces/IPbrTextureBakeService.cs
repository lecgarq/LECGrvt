using System;
using LECG.Core.Substance;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IPbrTextureBakeService
    {
        BakedTextureSet Bake(SubstanceMaterialEntry entry, BakeOptions options, Action<string>? log = null);
    }
}
