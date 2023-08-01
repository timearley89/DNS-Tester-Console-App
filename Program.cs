using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net;
using System.Collections.Immutable;

namespace DNS_Tester_Console_App
{
    internal class Program
    {   public static bool ThreadsComplete = false;
        static void Main(string[] args)
        {
            Console.WriteLine("DNS Tester 1.0"); Console.WriteLine("Downloads US DNS list from public-dns.info, and tests each one asynchronously to determine best ping time."); Console.WriteLine(@"Press 'Enter' to begin...");
            Console.ReadLine();
            GetDNSList();
            while (!ThreadsComplete) //MUST HAVE THIS to avoid main thread closing while awaiting ping responses.
            {
                Thread.Sleep(500);
                Console.Write(".");
            }
            Console.WriteLine("\nThanks for using DNS Tester! -Tim Earley");
        }
        
        static async void GetDNSList() //functional - downloads and filters dns servers from online public registry. 
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            string dnslistURL = @"https://public-dns.info/nameserver/us.csv";
            Console.WriteLine("Downloading IP List...");
            HttpClient myclient = new HttpClient();
            string namesvrs = myclient.GetStringAsync(dnslistURL).Result;
            sw.Stop();
            File.Delete("nameservers.csv");
            File.AppendAllText("nameservers.csv", namesvrs);
            Console.WriteLine($"Downloaded in {(double)sw.Elapsed.Milliseconds / 1000d} seconds. Extracting IPs...");
            sw.Reset();
            sw.Start();
            //split the string based on commas and NL chars.
            string[] nameServers = namesvrs.Split(new string[] { ",", "\n" }, StringSplitOptions.TrimEntries);
            sw.Stop();
            Console.WriteLine($"{nameServers.Length} fields applied in {(double)sw.Elapsed.Milliseconds / 1000d} seconds. Filtering...");
            sw.Reset();
            namesvrs = "";
            sw.Start();
            List<IPAddress> ipv4addresses = new List<IPAddress>();
            foreach (string s in nameServers)
            {
                IPAddress? thisIP;
                //compiler complains about possible null value, but if this value is null, the isvalidip method will return false, and so this ip won't be added.
                if (IsValidIP(s, out thisIP)) { ipv4addresses.Add(thisIP); }
            }
            //ipv4 addresses should now only contain valid ipv4 addresses as strings.
            nameServers = new string[ipv4addresses.Count];
            for (int i = 0; i < ipv4addresses.Count; i++)
            {
                nameServers[i] = ipv4addresses[i].ToString();
            }
            File.Delete("DNS Addresses.txt");
            File.AppendAllLines("DNS Addresses.txt", nameServers.ToArray());
            
            sw.Stop();
            Console.WriteLine($"Filter and display operations completed in {(double)sw.Elapsed.Milliseconds / 1000d} seconds. DNS list generated and saved.");
            List<Tuple<long, IPAddress>> pingtimes = new List<Tuple<long, IPAddress>>();
            sw.Reset();
            List<Task<Tuple<long, IPAddress>>> pingtasks = new List<Task<Tuple<long, IPAddress>>>();
            sw.Start();
            Console.WriteLine($"Pinging IPs...");
            foreach (IPAddress ip in ipv4addresses)
            {
                pingtasks.Add(PingDNSAsync(ip)); //creates new tasks for each ip address, and starts them
            }
           
            
            
            var results = await Task.WhenAll(pingtasks.ToArray()); //waits on all tasks to finish before continuing.
            sw.Stop();
            ThreadsComplete = true;
            Console.WriteLine($"{results.Length} tasks finished - completed in {sw.Elapsed.TotalSeconds} seconds.");
            foreach (Task<Tuple<long, IPAddress>> ptask in pingtasks) 
            {
                if (ptask.IsCompletedSuccessfully) { pingtimes.Add(ptask.Result); } 
            }
            Console.WriteLine($"{pingtimes.Count} Ping Operations Completed");
            pingtimes.ToArray();
            pingtimes.Sort((x, y) => x.Item1.CompareTo(y.Item1));
            int bestindex = -1;
            IPAddress? bestaddress = null;
            for (int i = 0; i < pingtimes.Count; i++)
            {
                if (pingtimes[i].Item1 != 0) { bestindex = i; bestaddress = pingtimes[i].Item2; break; }
            }
            Console.WriteLine($"{bestaddress} returned fastest, with a time of {pingtimes[bestindex].Item1}mS.");
            pingtasks.Clear();
            pingtimes.Clear();
            ipv4addresses.Clear();
            nameServers = new string[1];
        }
        public static bool IsValidIP(string strInput, out IPAddress? strIP) //functional - tests string to see if it is a valid ipv4 address
        {
            strIP = null;
            //return false if not exactly 4 groups after split operation based on '.' char.
            string[] groups = strInput.Split('.', StringSplitOptions.TrimEntries);
            if (groups.Length != 4) { return false; }
            byte outbyte;
            byte[] octets = new byte[4];
            for (int i = 0; i < groups.Length; i++)
            {
                if (byte.TryParse(groups[i], out outbyte)) { octets[i] = outbyte; } else { return false; }
                //tries to parse each substring as a byte (numerical 0-255), and if any of them fail, return false.
            }
            //if execution reaches here, we have successfully validated 4 bytes in IP format. return true after setting out to new IP.
            strIP = new IPAddress(octets);
            if (strIP == null) { return false; }
            return true;
        }
        static async Task<Tuple<long, IPAddress>> PingDNSAsync(IPAddress thisIP)
        {
            Ping thisping = new Ping();
            PingReply myreply = await thisping.SendPingAsync(thisIP);
            Tuple<long, IPAddress> myresult = new Tuple<long, IPAddress>(myreply.RoundtripTime, thisIP);
            return myresult;
        }
    }
}