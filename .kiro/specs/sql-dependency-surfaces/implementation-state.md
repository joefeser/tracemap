# SQL Dependency Surfaces Implementation State

Status: implemented

## Shipped Scope

- Added shared SQL-shape extraction expectations, .NET SQL-shape backfill, TypeScript direct SQL surfaces, JVM SQL-shape backfill, Python alignment, and combined SQL surface hardening.
- Updated path/reverse/diff/impact behavior where needed, with safe SQL metadata rendering and rule-backed limitations.

## Follow-Ups

- Deeper dialect parsing and ORM-specific precision should use focused adapter or shared-parser specs.
