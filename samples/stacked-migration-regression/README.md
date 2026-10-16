# Stacked migration regression

Independent public end-to-end acceptance fixture, not a copy of private source.
Expected: four operation groups (two HTTP, SQL command and bulk write), four with
candidate entry paths, five parameters grouped only under command execution.

The database chain combines Handles -> Shared property -> optional argument ->
inline construction -> wrapper -> nested field receiver -> Try-wrapped command.
The command combines String.Empty, literal concatenation, partially qualified
SqlClient, five Parameters.Add calls and complex value assignments. Two separate
HTTP chains serve as regression controls. The global DataStore is a negative decoy.

StackedMigrationRegressionTests removes each critical evidence layer independently
and checks that no path is invented. Reversed input must produce identical output.
Unknown base ancestry, parameter execution effects, runtime routes and database
contracts remain unproved. Four of four means this fixture's observed groups only,
not all behavior in an arbitrary application. No fixture method is executed.
