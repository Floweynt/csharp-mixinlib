# How Does Mixin work?
The mixin processor goes through several phases:

## Phase 1: Mixin scanning
In this phase, the mixin processor will look for classes annotated with `[Mixin]`, as well as member methods with proper annotations

## Phase 2: Mixin opcode selection
Each injection method (obtained from scanning) has some associated OpCode selectors. These are processed by scanning the `[Target]`-ed methods

Additionally, selected methods are validated for the injector to ensure that method signatures match

## Phase 3: Mixin method transformation
This is composed of two sub-phases
### Phase 3a: Mixin method remap
Each non-shadow method in a `[Mixin]` class is transformed so that it behaves like an instance method (if it is an instance method) or static method
(if it is a static method) of the target class(es). Instructions are transformed so that field access/calls work properly:
- Calling non-shadow gets translated to call to the remapped version of the non-shadow method
- Calling shadow gets transformed into a call to the target class instance method
- Non-shadow field access gets transformed into a lookup (since we can't actually inject fields)
- Shadow field access gets transformed into a load/store from the target's field
### Phase 3b: Target method transformation
The injector is executed, and bytecode is transformed in the target method.