# The assistant: answers cited to the numbers already on screen

napkin's assistant is a language model that reads what napkin has already put on screen and writes
in plain words about it — it never computes anything itself, and it never states a number napkin
did not already give it. Design: [`design/llm-assistant.md`](design/llm-assistant.md).

## What it can do

- **Ask…** (**Ctrl/Cmd+Shift+A**, or *Assistant → Ask…*): type a question about the piece on
  screen — a result a check left on it, a row of an open list, or a "how do I" question about
  napkin itself — and a note slides on with the answer. Under the answer, in mono, **From
  napkin:** shows the exact sentences the answer relied on, verbatim, so every number you read is
  one napkin already said.
- **Explain this result** (*Assistant → Explain this result*, or the *Explain* link beside a
  result in the part panel): the same note, with the question already written for you.
- **Sketch from words…** (*Assistant → Sketch from words…*): describe a small piece of furniture
  in a sentence and the assistant proposes rough planks — a name, a size and a spot for each — on
  a sheet with a tick beside every one. Nothing lands until you press Enter, and it draws as one
  step you can undo in one press. Firm up then treats the planks exactly as if you had drawn them
  yourself: it still works out the relationships, the stock and the sizes.

## What it will never do

- **State a number, size, count or citation you can't already find on screen.** A sentence that
  names one is refused, and the note shows the refusal in place of the sentence — the rest of the
  answer stands.
- **Change the drawing on its own.** A sketch proposal is only ever a list of napkin's own drawing
  actions, shown with a tick each; nothing is applied until you accept it, and it is one step to
  undo.
- **Give an opinion on safety, or say whether something needs a permit or an engineer.** The
  disclaimer under every answer says so, and there is no way to make the assistant say otherwise.
- **Look anything up.** It cannot read a file, reach the network, or ask any of napkin's own
  checking logic a question. It is handed only what is already on the note, and nothing else.
- **Remember anything.** Nothing is kept between questions, and nothing about a project is kept
  once napkin closes.

## Where the model runs

*Assistant → Where the model runs…* offers three choices:

- **None** — the assistant says how to add a model, and answers nothing until you do.
- **A program on this machine: Ollama, or llama.cpp's llama-server** — napkin talks to it over
  this machine's own loopback address and nothing else; a URL naming another machine is refused
  outright.
- **In napkin, on this Mac (MLX)** — on Apple silicon, napkin runs a model itself, in its own
  process, from a folder of files you choose; nothing is sent anywhere either way.

Every answer's last line names exactly where it ran, in words like *"Local: qwen3:4b-q4_K_M at
127.0.0.1:11434 — nothing leaves this machine."* napkin does not phone home, check for updates, or
send a crash report; the only outbound call under the local options is the one loopback request a
question makes, and there is none at all under MLX.

## Setting up a program on this machine

napkin does not download or bundle a model. To use the first local option:

1. Install Ollama from [ollama.com/download](https://ollama.com/download) (macOS, Windows or
   Linux) and start it — open the app, or run `ollama serve` in a terminal.
2. Pull a model: `ollama pull qwen3:4b-q4_K_M` gets Qwen3-4B at 2.6 GB, Apache-2.0. A machine with
   memory to spare can use `ollama pull qwen3:8b` instead; `ollama pull phi4-mini` is the
   MIT-licensed alternative.
3. In *Assistant → Where the model runs…*, press **Check** and pick the model from the list —
   napkin shows its size, quantization and license exactly as the program reports them.

llama.cpp's `llama-server` works the same way, at its own default address.

**One caveat.** Ollama can also run a model in its own cloud rather than on your machine. napkin
refuses a model Ollama itself reports as remote, but cannot promise every case is marked that way.
To be certain nothing ever leaves this machine through Ollama, set `OLLAMA_NO_CLOUD=1` before
starting it — see Ollama's own FAQ.

## A cloud option is planned, not built

A future, separate opt-in to send a question to Anthropic's Claude API with your own key is on
napkin's roadmap for this feature, off by default, with its own dialog naming exactly what would
be sent before you ever turn it on. It is not built yet: today, every model napkin can talk to
runs on your own machine, full stop, and the note's whereabouts line and this page will say so
plainly the day that changes.

## Checking answer quality yourself

The assistant's own guard — the rule that refuses an unsupported number — is proven by napkin's
usual tests. Whether a *particular* model's *wording* is any good is a different question, and it
is not something a build can pass or fail: a person has to read the answers. `dotnet run
--project tools/Napkin.Tools -- assistant eval --endpoint http://127.0.0.1:11434 --model
<name>` (or `--mlx <folder>` for the in-process runtime) runs twenty prepared questions through
your own model and prints how each one did — kept, refused, or unreadable, and how long each took.
It measures; it never blocks a build, and nothing about it runs on its own.
