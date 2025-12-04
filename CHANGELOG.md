# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

-   Initial release of NFury - API Load Testing Tool
-   Command-line interface for load testing with customizable parameters
-   Web-based dashboard with real-time monitoring via SignalR
-   SQLite database for storing test results and history
-   Support for multiple HTTP methods (GET, POST, PUT, DELETE, PATCH)
-   Virtual user simulation for concurrent request testing
-   Detailed metrics including response time percentiles (50th, 75th, 90th, 95th, 99th)
-   Status code distribution analysis
-   Interactive progress tracking during test execution
-   Project management for organizing multiple test configurations

### Features

-   Real-time test progress visualization
-   Historical test result comparison
-   Customizable request headers and body
-   Request timeout configuration
-   Virtual user count configuration
-   Request count configuration

## [1.0.0] - TBD

### Added

-   First stable release

[Unreleased]: https://github.com/unhackeddev/nfury/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/unhackeddev/nfury/releases/tag/v1.0.0
