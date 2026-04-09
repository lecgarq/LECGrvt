using LECG.Batch.Models;

namespace LECG.Batch.Services.Interfaces
{
    public interface IApsSessionStore
    {
        ApsSession? Load();
        void Save(ApsSession session);
        void Delete();
    }
}
