# Cobol Object Mapper

A Cobol (Copybook) parser that produces an corresponding class hierarchy
Christophe VG (<contact@christophe.vg>)  
[https://github.com/christophevg/cobol-object-mapper](https://github.com/christophevg/cobol-object-mapper)

## Introduction

This [Cobol parser](src/cobol.cs) for this project was generated using the [Human Parser Generator](https://github.com/christophevg/human-parser-generator). It produces an AST for copybook record definitions. These definitions are transformed in a basic class hierarchy, consisting of classes with properties and associations to other classes, thus mimicking the hierarchical copybook records. Redefining records are transformed into subclasses of the redefined record that is explicitly generated as an additional class.

**Disclaimer** I'm not aiming for feature completeness and only add support for what I need at a given time ;-)
