using core.Platform;
using core.Game;
using System.Collections.Generic;

namespace PlatformExtension
{
    public class CEpicPlatform : CPlatform
    {
        public CEpicPlatform(int id, string name, string description, string path, bool isActive)
            : base(id, name, description, path, isActive)
        {

        }

        public override bool GameLaunch(GameObject game)
        {
            throw new System.NotImplementedException();
        }

        public override HashSet<GameObject> GameScanner()
        {
            //throw new System.NotImplementedException();
            System.Console.WriteLine("This is the epic platform");
            return null;
        }
    }

    public class CEpicFactory : CPlatformFactory<CPlatform>
    {
        public override CPlatform CreateDefault()
        {
            return new CEpicPlatform(-1, GetPlatformName(), "", "", true);
        }

        public override CPlatform CreateFromDatabase(int id, string name, string description, string path, bool isActive)
        {
            return new CEpicPlatform(id, name, description, path, isActive);
        }

        public override string GetPlatformName()
        {
            return "Epic";
        }
    }
}
