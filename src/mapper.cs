// mapper to transform a Cobol copybook AST to a class hierarchy
// author: Christophe VG <contact@christophe.vg>

using System;
using System.IO;
using System.Collections.Generic;

using System.Linq;

namespace Cobol_Object_Mapper {

  class Model {
    public List<Class> Classes               { get; internal set; }
    public Model() {
      this.Classes = new List<Class>();
    }
    public override string ToString() {
      return string.Join("\n", this.Classes.Select(c => c.ToString()));
    }

    // Graphviz Dot output format support

    public string Dotify() {
      return @"
digraph G {
  node [
    fontname = ""Bitstream Vera Sans""
    fontsize = 10
    shape    = ""record""
  ]

  edge [
    fontname  = ""Bitstream Vera Sans""
    fontsize  = 8
    arrowhead = ""vee""
  ]
  
" +
        string.Join("\n", this.Classes.Select(c => c.Dotify()).ToList()) + "\n" +
        "  edge [ arrowhead = empty ]\n" +
        string.Join("\n", this.Classes.Select(
          c => (c.Super != null ? "  " + c.Name.Replace("-", "_") + " -> " + c.Super.Replace("-", "_") : "")
        ).ToList()) +
        "\n}\n";
    }
  }

  class Class {
    public string Name                       { get; internal set; }
    public string Super                      { get; internal set; }
    internal Stack<Property> PropertiesStack { get; set; }
    public List<Property> Properties         {
      get {
        return Enumerable.Reverse(this.PropertiesStack.ToList()).ToList();
      }
    }
    public List<Association> Associations    { get; internal set; }
    
    public Class() {
      this.PropertiesStack = new Stack<Property>();
      this.Associations    = new List<Association>();
    }

    public override string ToString() {
      return
        "- " + this.Name +
        ( this.Super != null ? " : " + this.Super : "" ) + 
        ( this.Properties.Count > 0 ?
          "\n" + string.Join("\n", this.Properties.Select(p => p.ToString())) 
          : "" ) + 
        ( this.Associations.Count > 0 ?
          "\n" + string.Join("\n", this.Associations.Select(a => a.ToString()))
          : "" );
    }

    public string Dotify() {
      // Class [
      //   label = "{Class|+ property : type\l ... |+ method() : void\l}"
      // ]
      return "  " + this.Name.Replace("-", "_") + " [\n    label = \"{" + this.Name + "|" +
        string.Join("", this.Properties.Select(p => p.Dotify())) + 
        "}\"\n  ]\n" +
        string.Join("\n", this.Associations.Select(a => a.Dotify())) + "\n";  
    }
  }
  
  class Property {
    public string Name                    { get; internal set; }
    public string Type                    { get; internal set; }
    public bool   Signed                  { get; internal set; }
    public override string ToString() {
      return "  - " + this.Name + " : " +
        ( this.Signed ? "signed " : "") + this.Type;
    }

    public string Dotify() {
      return "+ " + this.Name + " : " +
        ( this.Signed ? "signed " : "") + this.Type + "\\l";
    }  
  }

  class Association {
    public Class Source                   { get; internal set; }
    public string Target                  { get; internal set; }
    public string Multiplicity            { get; internal set; }
    public string DependsOn               { get; internal set; }
    public override string ToString() {
      return "  -> " + this.Target +
        ( this.Multiplicity == null ? "" :
          "[" + this.Multiplicity + "]" +
          ( this.DependsOn == null ? "" : "(" + this.DependsOn + ")" ));
    }

    public string Dotify() {
      if(this.Target == null) { return ""; } 
      return "  " + this.Source.Name.Replace("-", "_") +
             " -> " + this.Target.Replace("-", "_");
    }
  }

  class Mapper {
    public Model Model                    { get; private set; }
    
    public Mapper() {
      this.Model = new Model();
    }

    public Mapper Clear() {
      this.Model = new Model();
      return this;
    }

    public Mapper Parse(string input) {
      Copybook book = new Parser().Parse(input).AST;

      foreach(Record record in book.Records) {
        try {
          this.Import(
            new Dictionary<string,Func<Record,Imported>>() {
              { "RenamesRecord", this.ImportRenamesRecord },
              { "ValuesRecord",  this.ImportValuesRecord  },
              { "BasicRecord",   this.ImportBasicRecord   }
            }[record.GetType().ToString()](record)
          );
        } catch(KeyNotFoundException) {
          throw new ArgumentException(
            "Unknown record type: " + record.GetType().ToString()
          );
        }
      }

      // added "in progress" classes
      while(classes.Count > 0) {
        this.Model.Classes.Add(classes.Pop());
      }

      return this;
    }

    private enum Sorting { Ascending, Descending };

    private class Imported {
      public int          Level             { get; set; }
      public string       Name              { get; set; }
      public int          IntValue          { get; set; }
      public string       Redefines         { get; set; }
      public int          CompLevel         { get; set; }
      public string       Sign              { get; set; }
      public int          Amount            { get; set; }
      public int          MaxAmount         { get; set; }
      public string       AmountDependsOn   { get; set; }
      public string       Type              { get; set; }
      public int          TypeLength        { get; set; }
      public bool         TypeIsSigned      { get; set; }
      public int          TypeDecimalLength { get; set; }

      public bool IsFiller {
        get { return this.Name == null; }
      }
      public bool IsClass {
        get { return ! this.IsFiller && this.Type == null; }
      }
      public string Multiplicity {
        get {
          return this.Amount + this.MaxAmount == 0 ? null :
                 this.Amount.ToString() +
                 (this.MaxAmount > 0 ? ".." + this.MaxAmount.ToString() : "");
        }
      }
    }

    Stack<Class> classes = new Stack<Class>();
    Stack<int>   levels  = new Stack<int>();

    private void Import(Imported imported) {
      if( levels.Count == 0 ) { levels.Push(0); } // lazy init

      if( imported.Level <= levels.Peek() ) {
        // pop levels until we're at the level that imports this imported level
        while(levels.Peek() >= imported.Level) {
          levels.Pop();
          // finalize current class by adding it to the Model
          this.Model.Classes.Add(classes.Pop());
        }
      }

      if( imported.IsClass ) {
        // CLASS

        // add association to this new class on the current class, unless its 
        // the first one
        if( classes.Count > 0 ) {
          if( imported.Redefines != null ) {
            // TODO we only deal with redefines of the last property, check!
            if( classes.Peek().PropertiesStack.Count > 0 &&
                classes.Peek().PropertiesStack.Peek().Name
                  .Equals(imported.Redefines))
            {
              // transform property into association to a base class for 
              // redefining sub-classes (statement order is important)
              classes.Peek().PropertiesStack.Pop();                    // - prop
              classes.Peek().Associations.Add( new Association() {     // + asso
                Source = classes.Peek(),
                Target = imported.Redefines
              });
              classes.Push(new Class() { Name = imported.Redefines }); // + base
            }
          } else {
            classes.Peek().Associations.Add(new Association() {
              Source       = classes.Peek(),
              Target       = imported.Name,
              Multiplicity = imported.Multiplicity,
              DependsOn    = imported.AmountDependsOn
            });
          }
        }
        // add this class
        levels.Push(imported.Level);
        classes.Push(new Class() {
          Name  = imported.Name,
          Super = imported.Redefines != null ? imported.Redefines : null
        });
      } else {
        // Property
        if( ! imported.IsFiller ) {
          classes.Peek().PropertiesStack.Push(new Property() {
            Name   = imported.Name,
            Type   = imported.Type,
            Signed = imported.TypeIsSigned
          });
        }
      }
    }
    
    private Imported ImportRenamesRecord(Record record) {
      throw new NotImplementedException( "Renames records are not yet supported." );
    }

    private Imported ImportValuesRecord(Record record) {
      throw new NotImplementedException( "Values records are not yet supported." );
    }

    private Imported ImportBasicRecord(Record anyRecord) {
      BasicRecord record = anyRecord as BasicRecord;

      Imported imported = new Imported() {
        Level = Int32.Parse(record.Level.Value),
        Name  = (! record.LevelName.HasFiller ?
          record.LevelName.Identifier.Name : null)
      };

      // process options to extract Type and Super
      foreach(Option option in record.Options) {
        try {
          new Dictionary<string,Action<Option,Imported>>() {
            // ValueOption <|-- Symbolic <|--
            { "Variable",            this.ImportVariableValue       },
            //                           <|-- Figurative
            { "Zero",                this.ImportZeroValue           },
            { "Space",               this.ImportSpaceValue          },
            { "HighValue",           this.ImportHighValue           },
            { "LowValue",            this.ImportLowValue            },
            { "AllString",           this.ImportAllStringValue      },
            { "Null",                this.ImportNullValue           },
            //                           <|-- Literal
            { "Float",               this.ImportFloatValue          },
            { "String",              this.ImportStringValue         },
            { "Int",                 this.ImportIntValue            },
            //
            { "RedefinesOption",     this.ImportRedefinesOption     },
            { "ExternalOption",      this.ImportExternalOption      },
            { "InternalOption",      this.ImportInternalOption      },
            // UsageOption <|-- Usage <|--
            { "IndexUsage",          this.ImportIndexUsage          },
            { "PackedDecimalUsage",  this.ImportPackedDecimalUsage  },
            { "BinaryUsage",         this.ImportBinaryUsage         },
            { "CompUsage",           this.ImportCompUsage           },
            { "DisplayUsage",        this.ImportDisplayUsage        },
            //
            { "SignOption",          this.ImportSignOption          },
            { "OccursOption",        this.ImportOccursOption        },
            { "SyncOption",          this.ImportSyncOption          },
            { "JustOption",          this.ImportJustOption          },
            { "BlankOption",         this.ImportBlankOption         },
            // PictureOption <|--
            { "PictureStringOption", this.ImportPictureStringOption },
            { "PictureFormatOption", this.ImportPictureFormatOption }
          }[option.GetType().ToString()](option, imported);
        } catch(KeyNotFoundException) {
          throw new ArgumentException(
            "Unknown record option: " + option.GetType().ToString() + " from " +
              record.ToString()
          );
        }
      }
      return imported;
    }

    private void ImportVariableValue(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Variable value options are not yet supported." );
    }

    private void ImportZeroValue(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Zero value options are not yet supported." );
    }

    private void ImportSpaceValue(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Space value options are not yet supported." );
    }

    private void ImportHighValue(Option anyOption, Imported imported) {
      throw new NotImplementedException( "High value options are not yet supported." );
    }

    private void ImportLowValue(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Low value options are not yet supported." );
    }

    private void ImportAllStringValue(Option anyOption, Imported imported) {
      throw new NotImplementedException( "AllString value options are not yet supported." );
    }

    private void ImportNullValue(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Null value options are not yet supported." );
    }

    private void ImportFloatValue(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Float value options are not yet supported." );
    }

    private void ImportStringValue(Option anyOption, Imported imported) {
      throw new NotImplementedException( "String value options are not yet supported." );
    }

    private void ImportIntValue(Option anyOption, Imported imported) {
      Int option = anyOption as Int;
      imported.IntValue = Int32.Parse(option.Value);
    }

    private void ImportRedefinesOption(Option anyOption, Imported imported) {
      RedefinesOption option = anyOption as RedefinesOption;
      imported.Redefines = option.Redefined.Name;
    }

    private void ImportExternalOption(Option anyOption, Imported imported) {
      throw new NotImplementedException( "External options are not yet supported." );
    }

    private void ImportInternalOption(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Internal options are not yet supported." );
    }

    private void ImportIndexUsage(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Index usages are not yet supported." );
    }

    private void ImportPackedDecimalUsage(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Packed decimal usages are not yet supported." );
    }

    private void ImportBinaryUsage(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Binary usages are not yet supported." );
    }

    private void ImportCompUsage(Option anyOption, Imported imported) {
      CompUsage option = anyOption as CompUsage;
      imported.CompLevel = Int32.Parse(option.Level);
    }

    private void ImportDisplayUsage(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Display usages are not yet supported." );
    }

    private void ImportSignOption(Option anyOption, Imported imported) {
      SignOption option = anyOption as SignOption;
      imported.Sign = (option.HasLeading ? "leading" : "trailing" ) +
        (option.HasSeparate ? " separate" +
          (option.HasCharacter ? " character" : "") : "" );
    }

    private void ImportOccursOption(Option anyOption, Imported imported) {
      OccursOption option = anyOption as OccursOption;
      if(option.Amount != null) {
        if(option.Amount is Int) {
          imported.Amount = Int32.Parse(((Int)option.Amount).Value);
        } else {
          throw new NotImplementedException( "Identifier Amounts are not yet supported." );
        }
      }
      if(option.UpperBound != null) {
        if(option.UpperBound is Int) {
          imported.MaxAmount = Int32.Parse(((Int)option.UpperBound).Value);
        } else {
          throw new NotImplementedException( "Identifier UpperBounds are not yet supported." );
        }
      }
      if(option.DependsOn != null) {
        imported.AmountDependsOn = option.DependsOn.Name;
      }
      if(option.Keys.Count > 0) {
        throw new NotImplementedException( "Occurs Keys are not yet supported." );
      }
      if(option.Indexes.Count > 0) {
        throw new NotImplementedException( "Occurs Indexes are not yet supported." );
      }
    }

    private void ImportSyncOption(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Sync options are not yet supported." );
    }

    private void ImportJustOption(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Just options are not yet supported." );
    }

    private void ImportBlankOption(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Blank options are not yet supported." );
    }

    private void ImportPictureStringOption(Option anyOption, Imported imported) {
      throw new NotImplementedException( "Picture string options are not yet supported." );
    }

    private void ImportPictureFormatOption(Option anyOption, Imported imported) {
      PictureFormatOption option = anyOption as PictureFormatOption;

      // alphabetic or alphanumeric
      if(option.Type.StartsWith("A") || option.Type.StartsWith("X")) {
        imported.Type = "string";
        if(option.Digits != null) {
          imported.TypeLength = Int32.Parse(option.Digits.Value);
        }
      } else {
        // numeric
        if(option.Type.StartsWith("S")) {
          imported.TypeIsSigned = true;
        }
        if(option.DecimalType == null) {
          imported.Type = "integer";
        } else {
          imported.Type = "float";
          if(option.DecimalDigits != null) {
            imported.TypeDecimalLength = Int32.Parse(option.DecimalDigits.Value);
          }
        }
      }
    }

  }
}
