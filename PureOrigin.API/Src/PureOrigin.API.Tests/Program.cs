using System;
using System.Linq;
using System.Threading.Tasks;

using PureOrigin.API;

namespace ExampleTest
{
    class Program
    {
        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();
        public async Task MainAsync()
        {
            var api = new OriginAPI("email", "password");
            var result = await api.LoginAsync();
            if (result)
            {
                var self = api.InternalUser;
                Console.WriteLine("--- Self ---");
                Console.WriteLine($"Id: {self.UserId}");
                Console.WriteLine($"Email: {self.Email}");
                Console.WriteLine($"Language: {self.Language}");

                Console.WriteLine();
                Console.WriteLine();

                var user = await api.GetUserAsync("Munkeed");
                if (user != null)
                {
                    Console.WriteLine("--- Munkeed ---");
                    Console.WriteLine($"Id: {user.UserId}");
                    Console.WriteLine($"Username: {user.Username}");
                    Console.WriteLine($"PersonaId: {user.PersonaId}");
                    Console.WriteLine($"AvatarUrl: {await user.GetAvatarUrlAsync()}");

                    Console.WriteLine();
                    Console.WriteLine();
                }

                var users = await api.GetUsersAsync("Munk", 2);
                Console.WriteLine($"--- Search (Munk) User(s) ---");
                Console.WriteLine($"Count: {users.Count()}");
                foreach (var searchUser in users)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Id: {searchUser.UserId}");
                    Console.WriteLine($"Username: {searchUser.Username}");
                    Console.WriteLine($"PersonaId: {searchUser.PersonaId}");
                    Console.WriteLine($"AvatarUrl: {await searchUser.GetAvatarUrlAsync()}");
                }
            }
            Console.Read();
        }
    }
}