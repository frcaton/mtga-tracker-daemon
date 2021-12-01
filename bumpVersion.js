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
  var result = data.replace(
    "<AssemblyVersion>1</AssemblyVersion>",
    "<AssemblyVersion>" + process.argv.slice(2) + "</AssemblyVersion>"
  );

  fs.writeFile(csProjFile, result, "utf8", function (err) {
    if (err) return console.log(err);
  });
});
