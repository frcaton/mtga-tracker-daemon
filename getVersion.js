const fs = require("fs");
const path = require("path");

const csProjFile = path.join(
  __dirname,
  "src",
  "mtga-tracker-daemon",
  "mtga-tracker-daemon.csproj"
);

fs.readFile(csProjFile, "utf8", function (err, data) {
  if (err) {
    return console.log(err);
  }
  
  const versionRegexp = new RegExp("\<AssemblyVersion\>(.*)\<\/AssemblyVersion\>", "g");

  const match = versionRegexp.exec(data);

  console.log(match[1])
});
