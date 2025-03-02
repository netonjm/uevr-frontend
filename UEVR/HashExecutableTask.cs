//using Newtonsoft.Json;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Security.Cryptography;
//using System.Text;
//using System.Threading.Tasks;

//namespace UEVR
//{
//    internal class HashExecutableTask : Microsoft.Build.Utilities.Task
//    {
//        static string HashString(string s)
//        {
//            using (SHA256 sha256 = SHA256.Create())
//            {
//                byte[] hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
//                return BitConverter.ToString(hashedBytes).Replace("-", "").ToUpper();
//            }
//        }

//        public override bool Execute()
//        {
//            try
//            {
//                var directoryPath = Path.GetDirectoryName(typeof(HashExecutableTask).Assembly.Location);
//                // Read existing JSON file
//                string inputFilePath = Path.Combine(directoryPath, "PlainTextFilteredExecutables.json");
//                if (File.Exists(inputFilePath))
//                {
//                    string jsonContent = File.ReadAllText(inputFilePath);
//                    List<string> executables = JsonConvert.DeserializeObject<List<string>>(jsonContent);

//                    // Create a list to hold hashed executable names
//                    List<string> hashedExecutables = new List<string>();
//                    foreach (string executable in executables)
//                    {
//                        hashedExecutables.Add(HashString(executable));
//                    }

//                    // Save to a new JSON file
//                    string outputFilePath = Path.Combine(directoryPath, "FilteredExecutables.json");
//                    File.WriteAllText(outputFilePath, JsonConvert.SerializeObject(hashedExecutables, Formatting.Indented));
//                }
//                else
//                {
//                    Console.WriteLine("Error: Input file 'PlainTextFilteredExecutables.json' not found.");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error: {ex.Message}");
//            }
//            return true;
//        }
//    }
//}
