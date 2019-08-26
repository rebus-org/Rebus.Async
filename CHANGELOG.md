# Changelog

## 2.0.0-a1

* Test release

## 2.0.0-b01

* Test release

## 2.0.0

* Release 2.0.0

## 3.0.0

* Reduce interval between checking for reply (yields much lower latency)
* Change signature of the async bus extension to allow for optionally passing headers along with the request

## 4.0.0

* Update to Rebus 3

## 5.0.0

* Update to Rebus 4
* Add .NET Core support (netstandard 1.3)
* Change reply correlation method to be reliable by basing it on the new `rbs2-in-reply-to` header
* Dismantle ambient transaction context when blocking the callsite because it does not make sense to enlist request in a transaction

## 5.0.1

* Use _ in special message ID to make the library usable with Amazon SQS, where : causes trouble

## 6.0.0

* Use `TaskCompletionSource` to trigger completion, because it's the right thing to do - thanks [SaltyDH]
* Update target frameworks (.NET 4.6 and .NET Standard 2.0 respectively)

## 6.0.2

* Remove need for background cleaning, as all asynchronously awaited requests are guaranteed to either be completed or time out, in both cases removing the task completion source from the dictionary

[SaltyDH]: https://github.com/SaltyDH