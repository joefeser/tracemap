# Requirements: Web Forms annotated source view

1. When raw source is explicitly enabled, each private code-path review shall emit a complete annotated HTML view for every retained source file.
2. Each source line shall have a stable anchor, and retained handler, event-binding, declaration, and call-site spans shall be visually distinguishable.
3. The private graph, call path, and evidence sections shall link to exact source lines; annotated regions shall link back to retained evidence.
4. Source views shall use only authorized repository-relative paths beneath the supplied source root and shall preserve existing byte, path, symlink, and output-collision bounds.
5. Runs without raw-source opt-in shall emit no annotated source artifacts.
6. Anonymous HTML and JSON shall remain free of source text, paths, symbols, fact IDs, commit identity, and private source-view links.
7. Highlighting is navigation over retained static evidence and shall not claim runtime execution, branch feasibility, correctness, completeness, or migration intent.
