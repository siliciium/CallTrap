/*
Copyright 2026 Silicium

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*

PHONE_DIR/
    |
    ├── telecom/
    |     |
    │     ├── pb.vcf
    │     ├── ich.vcf
    │     ├── och.vcf
    │     ├── mch.vcf
    │     ├── cch.vcf
    |     |
    │     ├── pb/
    │     │    └── 0.vcf    
    │     ├── ich/
    │     │    └── 0.vcf
    │     ├── och/
    │     │    └── 0.vcf  
    │     ├── mch/
    │     │    └── 0.vcf
    │     └── cch/
    │          └── 0.vcf
    |
    ├── SIM1/
    │   └── telecom/
    |         |
    │         ├── pb.vcf
    │         ├── ich.vcf
    │         ├── och.vcf
    │         ├── mch.vcf
    │         ├── cch.vcf
    |         │    
    │         ├── pb/
    │         │    └── 0.vcf
    │         ├── ich/
    │         │    └── 0.vcf
    │         ├── och/
    │         │    └── 0.vcf
    │         ├── mch/
    │         │    └── 0.vcf
    │         └── cch/
    │             └── 0.vcf
    |
    └── SIM2/
        └── telecom/
              |
              ├── pb.vcf
              ├── ich.vcf
              ├── och.vcf
              ├── mch.vcf
              ├── cch.vcf
              │    
              ├── pb/
              │    └── 0.vcf
              ├── ich/
              │    └── 0.vcf
              ├── och/
              │    └── 0.vcf
              ├── mch/
              │    └── 0.vcf
              └── cch/
                   └── 0.vcf

- SAMSUNG
/int
/int/telecom
/int/telecom/pb
/int/telecom/ich
...

- XIAMOI
/int
/int/pb
/int/ich
/int/och
...

- Oppo / Realme
/int
/int/telecom

- Huawei
/int
/int/telecom

*/

namespace PhoneSim
{
    internal class PbapFileSystem
    {
        private class Contact
        {
            public string Name { get; }
            public string Number { get; }

            public Contact(string name, string number)
            {
                Name = name;
                Number = number;
            }

            public override string ToString() => $"{Name} ({Number})";
        }


        public static string RootPath { get; private set; }


        public static void Init()
        {
            // Root folder = executable path
            RootPath = Path.Combine(AppContext.BaseDirectory, "PHONE_DIR");

            // Roots PBAP
            // - /telecom/...
            // - /SIM1/telecom/...
            var roots = new[]
            {
                // non standard
                /*Path.Combine("int"),
                Path.Combine("int", "telecom"),*/
                Path.Combine("telecom"),
                Path.Combine("SIM1", "telecom"),
                Path.Combine("SIM2", "telecom")
            };

            string[] subdirs = { "pb", "ich", "och", "mch", "cch" };

            foreach (var root in roots)
            {
                string rootDir = Path.Combine(RootPath, root);
                if (!Directory.Exists(rootDir))
                {
                    Directory.CreateDirectory(rootDir);
                }


                // Sub folders (pb, ich, och, mch, cch)
                foreach (var sub in subdirs)
                {
                    string subDir = Path.Combine(rootDir, sub);
                    if (!Directory.Exists(subDir))
                    {
                        Directory.CreateDirectory(subDir);
                    }
                }

                // Generate .vcf files
                GeneratePhonebook(Path.Combine(rootDir, "pb.vcf"));
                GenerateIncomingCalls(Path.Combine(rootDir, "ich.vcf"));
                GenerateOutgoingCalls(Path.Combine(rootDir, "och.vcf"));
                GenerateMissedCalls(Path.Combine(rootDir, "mch.vcf"));
                GenerateCombinedCalls(Path.Combine(rootDir, "cch.vcf"));

                // Generate individual vCards in sub folders
                GenerateIndividualCards(Path.Combine(rootDir, "pb"), SampleContacts());
                GenerateIndividualCards(Path.Combine(rootDir, "ich"), SampleIncoming());
                GenerateIndividualCards(Path.Combine(rootDir, "och"), SampleOutgoing());
                GenerateIndividualCards(Path.Combine(rootDir, "mch"), SampleMissed());
                GenerateIndividualCards(Path.Combine(rootDir, "cch"), SampleCombined());
            }

            Console.WriteLine("[PBAP] File system initialized at: " + RootPath);
        }


        public static string ResolvePbapPath(string currentPath)
        {
            if (currentPath == "/")
                return RootPath;

            return Path.Combine(RootPath, currentPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        }

        public static string RootPlan()
        {
            return @"
PHONE_DIR/
    |
    ├── telecom/
    |     |
    │     ├── pb.vcf
    │     ├── ich.vcf
    │     ├── och.vcf
    │     ├── mch.vcf
    │     ├── cch.vcf
    |     |
    │     ├── pb/
    │     │    └── 0.vcf    
    │     ├── ich/
    │     │    └── 0.vcf
    │     ├── och/
    │     │    └── 0.vcf  
    │     ├── mch/
    │     │    └── 0.vcf
    │     └── cch/
    │          └── 0.vcf
    |
    └── SIM1/
          └── telecom/
                |
                ├── pb.vcf
                ├── ich.vcf
                ├── och.vcf
                ├── mch.vcf
                ├── cch.vcf
                │    
                ├── pb/
                │    └── 0.vcf
                ├── ich/
                │    └── 0.vcf
                ├── och/
                │    └── 0.vcf
                ├── mch/
                │    └── 0.vcf
                └── cch/
                     └── 0.vcf
";
        }

        public static string TelecomPath => Path.Combine(RootPath, "telecom");
        public static string Sim1Path => Path.Combine(RootPath, "SIM1");
        public static string Get_Phonebook_File(string root) => Path.Combine(RootPath, root, "pb.vcf");
        public static string Get_IncomingCallHist_File(string root) => Path.Combine(RootPath, root, "ich.vcf");
        public static string Get_OutgoingCallHist_File(string root) => Path.Combine(RootPath, root, "och.vcf");
        public static string Get_MissedCallHist_File(string root) => Path.Combine(RootPath, root, "mch.vcf");
        public static string Get_CombinedCallHist_File(string root) => Path.Combine(RootPath, root, "cch.vcf");


        // ------------------------------------------------------------
        //  Génération des vCards
        // ------------------------------------------------------------

        private static void GeneratePhonebook(string path)
        {
            WriteVcfList(path, SampleContacts());
        }

        private static void GenerateIncomingCalls(string path)
        {
            WriteVcfList(path, SampleIncoming());
        }

        private static void GenerateOutgoingCalls(string path)
        {
            WriteVcfList(path, SampleOutgoing());
        }

        private static void GenerateMissedCalls(string path)
        {
            WriteVcfList(path, SampleMissed());
        }

        private static void GenerateCombinedCalls(string path)
        {
            WriteVcfList(path, SampleCombined());
        }

        private static void WriteVcfList(string path, List<Contact> list)
        {
            if (!File.Exists(path))
            {
                StringBuilder sb = new StringBuilder();

                foreach (var c in list)
                    sb.AppendLine(BuildVCard(c));

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
        }

        private static void GenerateIndividualCards(string dir, List<Contact> list)
        {
            int index = 0;
            foreach (var c in list)
            {
                string file = Path.Combine(dir, $"{index}.vcf");
                File.WriteAllText(file, BuildVCard(c), Encoding.UTF8);
                index++;
            }
        }

        // ------------------------------------------------------------
        //  vCard builder
        // ------------------------------------------------------------

        private static string BuildVCard(Contact c)
        {
            return
    $@"BEGIN:VCARD
VERSION:3.0
FN:{c.Name}
TEL;CELL:{c.Number}
END:VCARD";
        }


        // ------------------------------------------------------------
        //  Données simulées
        // ------------------------------------------------------------

        private static List<Contact> SampleContacts() => new List<Contact>()
        {
            new Contact("Alice", "123456789"),
            new Contact("Bob", "987654321"),
            new Contact("Charlie", "555000111")
        };

        private static List<Contact> SampleIncoming() => new List<Contact>()
        {
            new Contact("Alice", "123456789"),
            new Contact("Bob", "987654321")
        };
        private static List<Contact> SampleOutgoing() => new List<Contact>()
        {
            new Contact("Charlie", "555000111")
        };

        private static List<Contact> SampleMissed() => new List<Contact>()
        {
            new Contact("Bob", "987654321")
        };

        private static List<Contact> SampleCombined() => new List<Contact>()
        {
            new Contact("Alice", "123456789"),
            new Contact("Bob", "987654321"),
            new Contact("Charlie", "555000111")
        };
    }
}
