# NFury - API Load Testing Tool

NFury is an open-source tool developed to efficiently and quickly perform load testing on APIs. With NFury, you can simulate multiple virtual users making requests to a target API and analyze important metrics such as average response time, status code distribution, etc.

## Features

- **Virtual User Simulation:** Create a defined number of virtual users to simulate traffic on your API.
- **Flexible Configuration:** Specify the API URL, HTTP method, and the number of requests to be made.
- **Detailed Analysis:** Obtain detailed metrics such as average response time, status code distribution, and percentiles.
- **Interactive Interface:** Track test progress in real-time with an interactive command-line interface.

## Installation

To install NFury, clone the repository and compile the project using the .NET CLI:

```bash
git clone https://github.com/your-username/nfury.git
cd nfury/src/NFury
dotnet build -c Release
```

## Usage

To start a load test, execute the following command:

```bash
./NFury --url=https://your-api.com/api --method=GET --requests=100 --virtualUsers=10
```

Replace `--url`, `--method`, `--requests`, and `--virtualUsers` with the desired values for your API.

## Example Output

After the load test is complete, you will see output similar to the following:

```
[Test duration: 532.45 ms]
[Requests: 187.8 req/s]
[Avg. Response Time: 54.12 ms]
[Pct 50: 51.00 ms]
[Pct 75: 63.00 ms]
[Pct 90: 71.00 ms]
[Pct 95: 78.00 ms]
[Pct 99: 98.00 ms]

[Results per Status Code]
┌───────────┬────────┬────────┬────────┬────────┬────────┬────────┬────────┬────────┐
│ Status    │ Min    │ Avg    │ Max    │ Pct 50 │ Pct 75 │ Pct 90 │ Pct 95 │ Pct 99 │
│ Code      │ (ms)   │ (ms)   │ (ms)   │ (ms)   │ (ms)   │ (ms)   │ (ms)   │ (ms)   │
├───────────┼────────┼────────┼────────┼────────┼────────┼────────┼────────┼────────┤
│ 200       │ 49.00  │ 54.12  │ 98.00  │ 51.00  │ 63.00  │ 71.00  │ 78.00  │ 98.00  │
│ 404       │ 56.00  │ 61.30  │ 92.00  │ 59.00  │ 67.00  │ 75.00  │ 81.00  │ 92.00  │
└───────────┴────────┴────────┴────────┴────────┴────────┴────────┴────────┴────────┘
```

## Contributing

Contributions are welcome! Feel free to open issues or send pull requests with improvements.

## License

This project is licensed under the [MIT License](LICENSE).

---

We hope NFury proves useful for your API load testing needs. If you have any questions or suggestions, feel free to contact us!

[GitHub](https://github.com/unhackeddev/nfury) | [Documentation](https://github.com/unhackeddev/nfury/wiki) | [Report an Issue](https://github.com/unhackeddev/nfury/issues)
