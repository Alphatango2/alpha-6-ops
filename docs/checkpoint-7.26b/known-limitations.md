# Checkpoint 7.26B known limitations

- The Sierra 4 v0.23.0 package is built and extraction-tested but is not automatically installed. LocalAppData migration has been exercised in isolated test roots, not against the user's live profile.
- The legacy profile is deliberately preserved after migration. Automated removal or consolidation of that rollback copy is outside this checkpoint.
- Internal `Alpha6Ops` namespaces, source project names, journal/database filenames and crash-report prefixes remain for compatibility. They are not user-facing package authority.
- The installer remains an unsigned per-user bootstrap compiled with the existing Windows .NET Framework compiler. Moving to a signed maintained installer toolchain remains future release work.
- Map V2 remains opt-in and depends on the separately installed Microsoft Edge WebView2 Evergreen Runtime. Classic map fallback remains required.
- Retention runs during ordinary product startup. User-selected diagnostic exports are never cleaned automatically, and retention does not compact flight-history or PIREP authority.
- Native simulator behavior continues to rely on the accepted 7.25A engineering evidence. The 7.26B identity/storage package has not been installed for a new native flight during this checkpoint.
- The branch contains consolidated 7.24C–7.25A work because those changes were present and intentionally preserved at checkpoint entry. Cousin-branch integration should use the final 7.26B commits as the reviewed baseline instead of replaying archived working directories.
- `7.27 — Premium UI Foundation` and Hero Globe work have not started.
