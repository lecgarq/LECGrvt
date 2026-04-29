using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class CadTempFileCleanupService : ICadTempFileCleanupService
    {
        public void Cleanup(string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
