<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>Iron Hulk - Dashboard</title>
  <script src="https://cdn.tailwindcss.com"></script>
</head>
<body class="bg-[#0f172a] text-white font-sans">

  <div class="flex h-screen overflow-hidden">
    <!-- Left Sidebar -->
    <aside class="w-64 bg-[#1e293b] p-5 flex flex-col space-y-6">
      <h1 class="text-2xl font-bold border-b border-gray-600 pb-2">Iron Hulk</h1>
      <nav class="space-y-2 text-blue-400">
        <button onclick="showSection('about')" class="block text-left w-full hover:underline">About Me</button>
        <button onclick="showSection('certifications')" class="block text-left w-full hover:underline">Certifications</button>
        <button onclick="showSection('projects')" class="block text-left w-full hover:underline">My Projects</button>
        <button onclick="showSection('contact')" class="block text-left w-full hover:underline">Contact</button>
      </nav>
    </aside>

    <!-- Main Dashboard -->
    <main id="mainDashboard" class="flex-1 p-6 overflow-y-auto space-y-6">
      <!-- Welcome Card -->
      <div class="bg-[#1f2937] p-6 rounded-xl flex justify-between items-center shadow-md">
        <div>
          <h2 class="text-xl font-bold"><span class="text-blue-400">Iron Hulk</span> 👋</h2>
          <p class="text-gray-400">Malware Developer | Red/Blue/Purple Teamer | CISM/CISSP</p>
        </div>
      </div>

      <!-- Embedded Image (Base64) -->
      <div class="bg-[#1f2937] p-6 rounded-xl shadow-md flex justify-center">
        <img 
          src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAA..." 
          alt="Iron Hulk" 
          class="max-w-full h-auto rounded-lg shadow-lg"
        />
      </div>
    </main>

    <!-- Dynamic Content Container -->
    <div id="dynamicContent" class="flex-1 p-6 overflow-y-auto space-y-6 hidden"></div>
  </div>

  <!-- JavaScript -->
  <script>
    function showSection(section) {
      const mainDashboard = document.getElementById("mainDashboard");
      const content = document.getElementById("dynamicContent");

      const data = {
        about: `
          <div class="bg-[#1f2937] p-6 rounded-xl shadow-md">
            <h3 class="text-2xl font-bold mb-2 text-blue-400">About Me</h3>
            <p class="text-gray-300 leading-relaxed">
              I'm <strong>Iron Hulk</strong>, a cybersecurity specialist and hacker with extensive experience in malware development,
              red/blue/purple teaming operations, and enterprise risk management. I hold certifications including
              <span class="text-green-400">CISM</span> and <span class="text-green-400">CISSP</span>. My focus is on developing stealthy attack vectors and defending high-value infrastructures.
            </p>
          </div>
        `,
        certifications: `
          <div class="bg-[#1f2937] p-6 rounded-xl shadow-md">
            <h3 class="text-2xl font-bold mb-2 text-blue-400">Certifications</h3>
            <ul class="list-disc ml-6 text-gray-300 space-y-1">
              <li>Certified Information Security Manager (CISM)</li>
              <li>Certified Information Systems Security Professional (CISSP)</li>
              <li>Offensive Security Certified Professional (OSCP)</li>
              <li>Red Team Professional (eCPTX / CRTP)</li>
            </ul>
          </div>
        `,
        projects: `
          <div class="bg-[#1f2937] p-6 rounded-xl shadow-md">
            <h3 class="text-2xl font-bold mb-2 text-blue-400">My Projects</h3>
            <ul class="list-inside text-gray-300 space-y-1">
              <li><strong>Process Spoofing Tool</strong> – Custom malware that spawns hidden processes with obfuscated parent IDs.</li>
              <li><strong>Domain Recon Suite</strong> – Toolkit for real-time Active Directory enumeration and privilege escalation analysis.</li>
            </ul>
          </div>
        `,
        contact: `
          <div class="bg-[#1f2937] p-6 rounded-xl shadow-md">
            <h3 class="text-2xl font-bold mb-2 text-blue-400">Contact</h3>
            <p class="text-gray-300">Email: <span class="text-green-400">iron.hulk@protonmail.com</span></p>
            <p class="text-gray-300">GitHub: <a href="https://github.com/ironhulk" class="underline text-blue-400">github.com/ironhulk</a></p>
          </div>
        `
      };

      mainDashboard.classList.add("hidden");
      content.classList.remove("hidden");
      content.innerHTML = data[section] || '';
    }
  </script>
</body>
</html>
