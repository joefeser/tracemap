# Design

`AccessDesignEvidenceComposer` creates event references from surface/control
properties. `AccessVbaProjector` resolves those references only within the
declared form/report module, then inspects the resolved procedure's existing
static effects for a literal save-current-record command. `AccessFactBuilder`
projects the result into event and command candidate facts using the existing
Access event/VBA rule IDs.

The text parser uses bounded state for multiline quoted values and records
opaque compound property shapes as gaps. It never opens Access, reads rows, or
executes VBA. All dynamic dispatch and unsupported property forms remain
partial evidence.
