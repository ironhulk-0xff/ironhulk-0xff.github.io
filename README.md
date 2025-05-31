<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Mista Hulk - About Me</title>
  <script src="https://cdn.tailwindcss.com"></script>
  <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
</head>
<body class="bg-[#12172b] text-white font-sans">
  <div class="flex h-screen">
    <!-- Sidebar -->
    <aside class="w-64 bg-[#1e2540] p-5 space-y-4">
      <h1 class="text-2xl font-bold">Mista Hulk</h1>
      <nav class="space-y-2">
        <a href="#" class="block text-blue-400">Dashboard</a>
        <a href="#" class="block">About</a>
        <a href="#" class="block">Projects</a>
        <a href="#" class="block">Certifications</a>
        <a href="#" class="block">Contact</a>
      </nav>
    </aside>

    <!-- Main Content -->
    <main class="flex-1 p-6 space-y-6 overflow-y-auto">
      <div class="bg-[#1f2937] p-6 rounded-xl flex justify-between items-center">
        <div>
          <h2 class="text-xl">Welcome back, <span class="text-blue-400">Mista Hulk</span> 👋</h2>
          <p class="text-gray-400">Malware Developer | Red/Blue/Purple Teamer | CISM/CISSP</p>
        </div>
        <div class="text-right">
          <p class="text-3xl font-bold">$65.4K</p>
          <p class="text-sm text-green-400">Today's Revenue</p>
        </div>
      </div>

      <div class="grid grid-cols-2 gap-6">
        <!-- Chart Example -->
        <div class="bg-[#1f2937] p-4 rounded-xl">
          <h3 class="mb-4 text-lg">Monthly Contributions</h3>
          <canvas id="barChart"></canvas>
        </div>

        <div class="bg-[#1f2937] p-4 rounded-xl">
          <h3 class="mb-4 text-lg">Tool Usage</h3>
          <canvas id="donutChart"></canvas>
        </div>
      </div>
    </main>
  </div>

  <script>
    new Chart(document.getElementById("barChart"), {
      type: 'bar',
      data: {
        labels: ["Jan", "Feb", "Mar", "Apr", "May"],
        datasets: [{
          label: "Contributions",
          data: [10, 20, 30, 50, 40],
          backgroundColor: "#3b82f6"
        }]
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
      }
    });
  </script>
</body>
</html>
