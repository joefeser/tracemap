package com.tracemap.jvm.scan;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public final class AnalysisGapCollector {
    private final List<String> gaps = new ArrayList<>();

    public void add(String gap) {
        if (gap != null && !gap.isBlank()) {
            gaps.add(gap);
        }
    }

    public List<String> gaps() {
        return Collections.unmodifiableList(gaps);
    }

    public boolean hasGaps() {
        return !gaps.isEmpty();
    }
}
