${
    // Enable extension methods by adding using Typewriter.Extensions.*
    // Make sure to created the generated folder
    using Typewriter.Extensions.Types;
    using System.IO;
        
    Template(Settings settings)
    {
        settings.OutputFilenameFactory = file =>
        {
            //if(!Directory.Exists("./generated"))
            //    Directory.CreateDirectory("./generated");
            return $"../../schedularClient/generated/{Path.GetFileNameWithoutExtension(file.Name)}.ts";
        };
    }

    // Custom extension methods can be used in the template by adding a $ prefix e.g. $LoudName
    string LoudName(Property property)
    {
        return property.Name.ToUpperInvariant();
    }
    
    bool IsIncluded(Property c)
    {
        if(c.Attributes.Any(a => String.Equals(a.name, "JsonIgnore", StringComparison.OrdinalIgnoreCase))){
            return false;
        }
        
        return true;
    }

    bool exportAsAny(Property c){
        
        if(c.Attributes.Any(a => String.Equals(a.name, "ExportAsAny", StringComparison.OrdinalIgnoreCase))){
            return true;
        }
        
        return false;
    }

    string exportedType(Property c){
        
        if(exportAsAny(c))
            return "any";
        else
            return c.Type.Name;
    }

    string exportedAsOptional(Property c){
        
        if(c.Attributes.Any(a => String.Equals(a.name, "ExportAsOptional", StringComparison.OrdinalIgnoreCase))){
            return "?";
        }
        
        return "";
    }


    string Inherit(Class c)
    {
        if (c.BaseClass!=null)
	        return " extends " + c.BaseClass.ToString();
          else
	         return  "";
    }



    string Imports(Class c){

      List<string> neededImports = c.Properties
	    .Where(p => (!p.Type.IsPrimitive || p.Type.IsEnum) && IsIncluded(p) && !exportAsAny(p))
	    .Select(p => "import { " + p.Type.Name.TrimEnd('[',']') + " } from './" + p.Type.Name.TrimEnd('[',']') + "';").ToList();


      if (c.BaseClass != null) { 
	    neededImports.Add("import { " + c.BaseClass.Name +" } from './" + c.BaseClass.Name + "';");
      }

      foreach(var attrib in c.Attributes.Where(a => String.Equals(a.name, "ForceTypeImport", StringComparison.OrdinalIgnoreCase))){
            neededImports.Add("import { " + attrib.Value +" } from './" + attrib.Value + "';");
      }


      return String.Join("\n", neededImports.Distinct());
    }

}
/*  GENERTAED automatically using Models.tst. DO NOT CHANGE HERE */
$Classes(*Model)[$Imports
//Generated from class $FullName
export interface $Name$TypeParameters$Inherit  {

    $Properties($IsIncluded)[$name$exportedAsOptional: $exportedType;]
    }

]

$Enums(*Model)[
//Generated from class $FullName
export enum $Name  {
$Values[
    $Name = '$Name'][,]
}]

