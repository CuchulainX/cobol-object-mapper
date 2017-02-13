// main demo application driver for Cobol Object Mapper
// author: Christophe VG <contact@christophe.vg>

using System;
using System.IO;
using System.Collections.Generic;

using System.Linq;

namespace Cobol_Object_Mapper {

  public class Importer {
    public static void Main(string[] args) {

      string input = null;

      if(args.Length > 0) {
        foreach(string filename in args) {
          if( ! File.Exists(filename) ) {
            Console.Error.WriteLine("WARNING: Unknown file: " + filename);
          } else {
            Console.Error.WriteLine("*** Importing " + filename);
            input += System.IO.File.ReadAllText(filename);
          }
        }
      }

      // no input from files ... try stdin
      if(input == null) {
        Console.Error.WriteLine("*** Reading from stdin...");
        string s;
        while( (s = Console.ReadLine()) != null ) {
          input += s;
        }
      }
    
      if(input == null) {
        Console.Error.WriteLine("ERROR: No input detected.");
        return;
      }
      
      Console.Error.WriteLine("*** Mapping...");
      Mapper mapper = new Mapper();
      mapper.Parse(input);

      Console.WriteLine(mapper.Model.ToString());
    }

  }
}
