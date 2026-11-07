# Included JavaScript click fixture

`Page.aspx` declares a click under `EditorController`. The content slot in
`Site.master` supplies the AngularJS app scope and renders the bundle declared
in `Global.asax`. Its three members define the module alias and two controllers.
Both controllers assign a method named `save`. The candidate for this page
must point to line 2 in `Scripts/editor.js`.

After scanning this directory, this query displays the two endpoints and
the retained evidence for the relationship.

```sql
SELECT click.file_path AS page, click.start_line AS click_line,
       handler.file_path AS script, handler.start_line AS method_line,
       json_extract(handler.properties_json, '$.controller') AS controller,
       json_extract(handler.properties_json, '$.method') AS method,
       json_extract(handler.properties_json, '$.supportingFactIds') AS evidence
FROM facts handler JOIN facts click
  ON click.fact_id = json_extract(handler.properties_json, '$.clickFactId')
WHERE handler.fact_type = 'WebFormsAngularClickHandlerCandidate';
```

Expected result is one row connecting `Page.aspx` line 4 to
`Scripts/editor.js` line 2. Owner facts name the page controller and master
module scope, JS module alias and registration. Bundle facts identify the
selected file by its exact declaration and member ID. This projectless
fixture supports a syntax candidate. Browser and HTTP execution are separate.

The script in Page.aspx lines 7–9 contains three single-line server output
initializers. Each contributes a WebFormsInlineServerInitializerCandidate
with valueStatus `unknown` and a supporting ID on the external handler.

Scripts/editor.js registers three controllers in one top-level call chain.
The page selects the first. Regression tests exercise the middle and last
registrations as well. Each exposes its own save function.
