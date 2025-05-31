<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Mista Hulk - Dashboard</title>
  <script src="https://cdn.tailwindcss.com"></script>
  <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
</head>
<body class="bg-[#0f172a] text-white font-sans">

  <div class="flex h-screen overflow-hidden">
    <!-- Left Sidebar -->
    <aside class="w-64 bg-[#1e293b] p-5 flex flex-col space-y-6">
      <h1 class="text-2xl font-bold border-b border-gray-600 pb-2">Mista Hulk</h1>
      <nav class="space-y-2 text-blue-400">
        <a href="#" class="block hover:underline">Dashboard</a>
        <a href="#" class="block hover:underline">About</a>
        <a href="#" class="block hover:underline">Projects</a>
        <a href="#" class="block hover:underline">Certifications</a>
        <a href="#" class="block hover:underline">Contact</a>
      </nav>
    </aside>

    <!-- Main Dashboard -->
    <main class="flex-1 p-6 mr-72 overflow-y-auto space-y-6">
      <!-- Welcome Card -->
      <div class="bg-[#1f2937] p-6 rounded-xl flex justify-between items-center shadow-md">
        <div>
          <h2 class="text-xl font-bold">Welcome back, <span class="text-blue-400">Mista Hulk</span> 👋</h2>
          <p class="text-gray-400">Malware Developer | Red/Blue/Purple Teamer | CISM/CISSP</p>
        </div>
        <div class="text-right">
          <p class="text-3xl font-bold">$65.4K</p>
          <p class="text-sm text-green-400">Today's Revenue</p>
        </div>
      </div>

      <!-- Chart Section -->
      <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
        <!-- Bar Chart -->
        <div class="bg-[#1f2937] p-4 rounded-xl shadow-md">
          <h3 class="text-lg font-semibold mb-2">Monthly Contributions</h3>
          <canvas id="barChart" height="200"></canvas>
        </div>

        <!-- Donut Chart -->
        <div class="bg-[#1f2937] p-4 rounded-xl shadow-md">
          <h3 class="text-lg font-semibold mb-2">Tool Usage</h3>
          <canvas id="donutChart" height="200"></canvas>
        </div>
      </div>
    </main>

    <!-- Right Panel -->
    <aside class="fixed top-0 right-0 w-72 h-full bg-[#1e2540] p-6 overflow-y-auto shadow-2xl">
      <div>
        <h2 class="text-xl font-bold border-b border-gray-500 pb-2">About Me</h2>
        <p class="text-sm text-gray-300 mt-2">
          I'm Mista Hulk — a cybersecurity engineer and hacker specializing in malware development,
          red/purple teaming, and enterprise security (CISM, CISSP).
        </p>
      </div>

      <div class="mt-8">
        <h2 class="text-xl font-bold border-b border-gray-500 pb-2">My Projects</h2>
        <ul class="mt-4 space-y-4 text-sm">
          <li>
            <button onclick="toggle('malwareDev')" class="w-full text-left text-blue-400 hover:underline">Malware Developer ▾</button>
            <ul id="malwareDev" class="ml-4 mt-1 space-y-1 hidden">
              <li>- Create a new process</li>
            </ul>
          </li>
          <li>
            <button onclick="toggle('activeDir')" class="w-full text-left text-blue-400 hover:underline">Active Directory ▾</button>
            <ul id="activeDir" class="ml-4 mt-1 space-y-1 hidden">
              <li>- Domain Enumeration</li>
            </ul>
          </li>
        </ul>
      </div>
    </aside>
  </div>

  <!-- JS Section -->
  <script>
    const toggle = (id) => {
      const section = document.getElementById(id);
      section.classList.toggle("hidden");
    };

    new Chart(document.getElementById("barChart"), {
      type: 'bar',
      data: {
        labels: ["Jan", "Feb", "Mar", "Apr", "May"],
        datasets: [{
          label: "Contributions",
          data: [10, 20, 30, 50, 40],
          backgroundColor: "#3b82f6"
        }]
      },
      options: {
        responsive: true,
        plugins: {
          legend: { display: false }
        },
        scales: {
          y: { beginAtZero: true }
        }
      }
    });

    new Chart(document.getElementById("donutChart"), {
      type: 'doughnut',
      data: {
        labels: ["VS Code", "Nmap", "Metasploit"],
        datasets: [{
          data: [45, 25, 30],
          backgroundColor: ["#6366f1", "#10b981", "#f59e0b"]
        }]
      },
      options: {
        responsive: true,
        plugins: {
          legend: {
            labels: {
              color: 'white'
            }
          }
        }
      }
    });
  </script>
</body>
</html>
