package com.tracemap.jvm.util;

import java.util.ArrayList;
import java.util.List;

public final class LineMap {
    private final List<Integer> lineStarts;

    private LineMap(List<Integer> lineStarts) {
        this.lineStarts = lineStarts;
    }

    public static LineMap from(String text) {
        List<Integer> starts = new ArrayList<>();
        starts.add(0);
        for (int i = 0; i < text.length(); i++) {
            if (text.charAt(i) == '\n') {
                starts.add(i + 1);
            }
        }
        return new LineMap(starts);
    }

    public int lineForOffset(long offset) {
        int value = (int) Math.max(0, offset);
        int low = 0;
        int high = lineStarts.size() - 1;
        while (low <= high) {
            int mid = (low + high) >>> 1;
            if (lineStarts.get(mid) <= value) {
                low = mid + 1;
            } else {
                high = mid - 1;
            }
        }
        return Math.max(1, high + 1);
    }
}
