package com.tracemap.jvm;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;

import com.tracemap.jvm.facts.FactFactory;
import java.util.Map;
import org.junit.jupiter.api.Test;

final class FactFactoryTest {
    @Test
    void factIdIsStableAndPropertyOrderIndependent() {
        String first = FactFactory.createFactId(
            "scan-1",
            "PropertyAccessed",
            "jvm.java.semantic.memberaccess.v1",
            "src/A.java",
            10,
            10,
            null,
            "a.Source",
            "a.Target.status",
            "Target.status",
            Map.of("propertyName", "status", "containingType", "a.Target"));
        String second = FactFactory.createFactId(
            "scan-1",
            "PropertyAccessed",
            "jvm.java.semantic.memberaccess.v1",
            "src/A.java",
            10,
            10,
            null,
            "a.Source",
            "a.Target.status",
            "Target.status",
            Map.of("containingType", "a.Target", "propertyName", "status"));
        String changed = FactFactory.createFactId(
            "scan-1",
            "PropertyAccessed",
            "jvm.java.semantic.memberaccess.v1",
            "src/A.java",
            10,
            10,
            null,
            "a.Source",
            "a.Target.status",
            "Target.status",
            Map.of("containingType", "a.Target", "propertyName", "state"));

        assertEquals(first, second);
        assertNotEquals(first, changed);
    }
}
